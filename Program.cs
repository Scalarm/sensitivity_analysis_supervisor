using System;
using SensitivityAnalysis;
using SensitivityAnalysis.MorrisDesign;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace sensitivity_analysis
{
	class Config
	{
		public string experiment_id { get; set; }
		public string experiment_manager_url { get; set; }
		public string experiment_manager_proxy_path { get; set; }
		public Dictionary<string, double[]> parameters { get; set; }
		public int morris_samples_count { get; set; }
		public int morris_level_count { get; set; }
	}

	class MainClass
	{
		public static double[] TestFunction(double[] x)
		{
			double y = 5 * x [0]
				+ x [1] * x [1]
				+ 10 * x [2];

			return new double[] { y };
		}

//		public static JObject ParseConfig(string configText)
//		{
//			return JObject.Parse(configText);
//		}

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

			string experimentId = appConfig["experiment_id"].ToObject<string>();
			string experimentManagerUrl = appConfig["experiment_manager_url"].ToObject<string>();
			string experimentManagerProxyPath = appConfig["experiment_manager_proxy_path"].ToObject<string>();
			var parameters = appConfig["parameters"].ToObject<ScalarmParameter[]>();
			var experimentOutputs = appConfig["outputs"].ToObject<string[]>();
			int morrisSamplesCount = appConfig["morris_samples_count"].ToObject<int>();
			int morrisLevelsCount = appConfig["morris_levels_count"].ToObject<int>();

			// TODO: LOAD CONFIG
//			var experimentId = appConfig.experiment_id;
//			var experimentManagerUrl = appConfig.experiment_manager_url;
//			var morrisSamplesCount = appConfig.morris_samples_count;
//			var morrisLevelsCount = appConfig.morris_level_count;

			// create Scalarm Client basing on credentials from script config
			Scalarm.Client client = null;
			if (!String.IsNullOrEmpty(experimentManagerProxyPath)) {
				client = new Scalarm.ProxyCertClient(experimentManagerUrl, new FileStream(experimentManagerProxyPath, FileMode.Open));
			} else {
				// TODO: support for experiment manager login/password
				throw new NotImplementedException("Experiment Manager login password auth");
			}

			// TODO!
//			Scalarm.SupervisedExperiment experiment = client.GetExperimentById<Scalarm.SupervisedExperiment>(experimentId);

			// generate inputs
			// TODO: get parameters ids and ranges
			// assume:

			// TODO: assuming that key order is the same as in config file
			InputProperties[] properties = new InputProperties[parameters.Count()];
			{
				for (int i=0; i<parameters.Count(); ++i) {
					properties[i] = new InputProperties(InputValuesType.Range, parameters[i].range);
				}
			}
//			properties[0] = new InputProperties(InputValuesType.Range, new double[] {0, 100});
//			properties[1] = new InputProperties(InputValuesType.Range, new double[] {-100, 100});
//			properties[2] = new InputProperties(InputValuesType.Range, new double[] {0, 100});
			List<MorrisDesignInput> inputs = MorrisDesignCore.GenerateInputs(properties, morrisSamplesCount, morrisLevelsCount);

			// TODO fake
			IList<Scalarm.SimulationParams> scalarmResults = new List<Scalarm.SimulationParams>();

			// TODO: add points
			foreach (MorrisDesignInput morrisPoint in inputs) {
				var pointInput = new Scalarm.ValuesMap();
				for (int i=0; i<parameters.Count(); ++i) {
					pointInput.Add(parameters[i].id, morrisPoint.Input[i]);
				}

				// TODO: mass-scheduling
				// TODO: real
				// experiment.SchedulePoint(pointInput);


				// TODO: fake - this will be done by Scalarm: SchedulePoint, add resources, wait, GetResults -> scalarmResults
				scalarmResults.Add(new Scalarm.SimulationParams(
					pointInput,
					new Scalarm.ValuesMap() {
						{"result", TestFunction(morrisPoint.Input.ToArray())[0]}
					}));
			}

			// start HPC resources
			// that was only for testing, resources should be added manually
			//IList<Scalarm.PrivateMachineCredentials> machinesList = client.GetPrivateMachineCredentials("jack.metal.agh.edu.pl", "scalarm");
			//Scalarm.PrivateMachineCredentials machine = machinesList[0];
			// experiment.SchedulePrivateMachineJobs(1, machine);

//			experiment.WaitForDone();

//			IList<Scalarm.SimulationParams> scalarmResults = experiment.GetResults();

			List<MorrisDesignOutput> outputs = new List<MorrisDesignOutput>();

			string[] ids = parameters.Select(p => p.id).ToArray();

			foreach (MorrisDesignInput morrisPoint in inputs) {
				var res = scalarmResults.Where(r => Enumerable.SequenceEqual(r.Input.Flatten(ids).Select(x => (double) x), morrisPoint.Input));

//				var x = input.Input[0];
//				var y = input.Input[1];
//				var z = input.Input[2];
//
//				// search for point sample in Scalarm-fetched results
//				var res = scalarmResults.Where(r => (Convert.ToDouble(r.Input["x"]) == x) && (Convert.ToDouble(r.Input["y"]) == y) && (Convert.ToDouble(r.Input["z"]) == z));

				if (res.Any()) {
					// TODO: handle simulation error - do not add these output values

					var output = res.First().Output;
					var morrisRes = new List<Double>(experimentOutputs.Select(i => (double) output[i]));

					outputs.Add(new MorrisDesignOutput(morrisPoint.InputId, morrisRes));
				} else {
					Console.WriteLine(String.Format("Result not found for {0}", morrisPoint.InputId.InputId));
				}
			}

			var notCalculated = BaseSa.GetNotCalculatedInputs<MorrisDesignInput, MorrisDesignOutput>(inputs, outputs);
			
			MorrisDesignSettings settings = new MorrisDesignSettings(properties, 10, 20);
			
			var results = MorrisDesignCore.CalculateSensitivity(settings, inputs, outputs);
			
			MorrisDesignSensitivityAnalysisResult result1 = results [0];
			result1.SaveResultsToTXTFile("wyniki.txt");

			// TODO: set experiment results in Scalarm, in future branch: POST set_result(result), POST mark_as_complete; new version: POST mark_as_complete(result)
		}
	}
}
