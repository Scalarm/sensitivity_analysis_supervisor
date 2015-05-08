using System;
using SensitivityAnalysis;
using SensitivityAnalysis.MorrisDesign;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace sensitivity_analysis
{
	class MainClass
	{
//		public static double[] TestFunction(double[] x)
//		{
//			double y = 5 * x [0]
//				+ x [1] * x [1]
//				+ 10 * x [2];
//
//			return new double[] { y };
//		}

		public static IDictionary<string, object> MorrisExperimentResults(MorrisDesignSensitivityAnalysisResult result, string[] paramIds)
		{
			//result1.SaveResultsToTXTFile("wyniki.txt");

			var normalizedAbsoluteMean = new Dictionary<string, double>();

			var morrisExperimentResults = new Dictionary<string, object>() {
				{"normalized_absolute_mean", normalizedAbsoluteMean}
			};

			foreach (SensitivityValue sv in result.NormalizedAbsoluteMean) {
				string paramId = paramIds[sv.ParameterId];
				double value = sv.Value;
				normalizedAbsoluteMean.Add(paramId, value);
			}

			return morrisExperimentResults;
		}


		public class ScalarmParameter
		{
			public string id;
			public double[] range;
		}

		public static void Main(string[] args)
		{
			// TODO: allow read config from stdin
			string configText = System.IO.File.ReadAllText("config.json");

			JObject appConfig = JObject.Parse(configText);

			// Scalarm and Morris Design config
			string experimentId = appConfig["experiment_id"].ToObject<string>();
			string experimentManagerUrl = appConfig["experiment_manager_url"].ToObject<string>();
			var parameters = appConfig["parameters"].ToObject<ScalarmParameter[]>();
			var outputIds = appConfig["output_ids"].ToObject<string[]>();
			int morrisSamplesCount = appConfig["morris_samples_count"].ToObject<int>();
			int morrisLevelsCount = appConfig["morris_levels_count"].ToObject<int>();
			// ---

			// create Scalarm Client basing on credentials from script config
			Scalarm.Client client = null;
			if (appConfig["experiment_manager_proxy_path"] != null) {
				string experimentManagerProxyPath = appConfig["experiment_manager_proxy_path"].ToObject<string>();
				client = new Scalarm.ProxyCertClient(experimentManagerUrl, new FileStream(experimentManagerProxyPath, FileMode.Open));
			} else {
				string experimentManagerLogin = appConfig["experiment_manager_login"].ToObject<string>();
				string experimentManagerPassword = appConfig["experiment_manager_password"].ToObject<string>();
				client = new Scalarm.BasicAuthClient(experimentManagerUrl, experimentManagerLogin, experimentManagerPassword);
			}
			// ---

			Scalarm.SupervisedExperiment experiment = client.GetExperimentById<Scalarm.SupervisedExperiment>(experimentId);

			// assuming that key order is the same as in config file
			InputProperties[] properties = new InputProperties[parameters.Count()];
			{
				for (int i=0; i<parameters.Count(); ++i) {
					properties[i] = new InputProperties(InputValuesType.Range, parameters[i].range);
				}
			}

			List<MorrisDesignInput> inputs = MorrisDesignCore.GenerateInputs(properties, morrisSamplesCount, morrisLevelsCount);
			IList<Scalarm.ValuesMap> pointsToSchedule = new List<Scalarm.ValuesMap>(inputs.Count);

			foreach (MorrisDesignInput morrisPoint in inputs) {
				var pointInput = new Scalarm.ValuesMap();
				for (int i=0; i<parameters.Count(); ++i) {
					pointInput.Add(parameters[i].id, morrisPoint.Input[i]);
				}

				pointsToSchedule.Add(pointInput);
			}

			experiment.SchedulePoints(pointsToSchedule);

			// start HPC resources
			// that was only for testing, resources should be added manually
			IList<Scalarm.PrivateMachineCredentials> machinesList = client.GetPrivateMachineCredentials("jack.metal.agh.edu.pl", "scalarm");
			Scalarm.PrivateMachineCredentials machine = machinesList[0];
			experiment.SchedulePrivateMachineJobs(4, machine);

			// block until results are available
			// also exceptions can be thrown if there are no resources
			while (true) {
				try {
					experiment.WaitForDone();
					break;
				} catch (Exception e) {
					Console.WriteLine("An exception was throw when waiting for results: {0}", e.ToString());
					Console.WriteLine("Waiting 5 seconds to retry...");
					Thread.Sleep(5000);
				}
			}

			IList<Scalarm.SimulationParams> scalarmResults = experiment.GetResults();

			List<MorrisDesignOutput> morrisOutputs = new List<MorrisDesignOutput>();

			string[] ids = parameters.Select(p => p.id).ToArray();

			foreach (MorrisDesignInput morrisPoint in inputs) {
				// find Scalar results for morrisPoint
				// TODO: optimize: store flatten Scalarm input in dictionary
				var res = scalarmResults.Where(r => Enumerable.SequenceEqual(r.Input.Flatten(ids).Select(x => Convert.ToDouble(x)), morrisPoint.Input));

				if (res.Any()) {
					// TODO: handle simulation error - do not add these output values

					// Scalarm output dictionary
					var output = res.First().Output;

					// flatten results with conversion to double
					var morrisRes = new List<Double>(output.Flatten(outputIds).Select(x => Convert.ToDouble(x)));

					morrisOutputs.Add(new MorrisDesignOutput(morrisPoint.InputId, morrisRes));
				} else {
					Console.WriteLine(String.Format("Result not found for {0}", morrisPoint.InputId.InputId));
				}
			}

			var notCalculated = BaseSa.GetNotCalculatedInputs<MorrisDesignInput, MorrisDesignOutput>(inputs, morrisOutputs);
			
			MorrisDesignSettings settings = new MorrisDesignSettings(properties, morrisSamplesCount, morrisLevelsCount);
			
			var results = MorrisDesignCore.CalculateSensitivity(settings, inputs, morrisOutputs);
			
			MorrisDesignSensitivityAnalysisResult result1 = results[0];
			//result1.SaveResultsToTXTFile("wyniki.txt");

			experiment.MarkAsComplete(JsonConvert.SerializeObject(MorrisExperimentResults(result1, ids)));

			// TODO: set experiment results in Scalarm, in future branch: POST set_result(result), POST mark_as_complete; new version: POST mark_as_complete(result)
		}
	}
}
