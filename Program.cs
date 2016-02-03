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
		// Some old test function; kept here to show function example
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
			var normalizedAbsoluteMean = new Dictionary<string, double>();
			var normalizedMean = new Dictionary<string, double>();
			var normalizedAbsoluteStandardDeviation = new Dictionary<string, double>();
			var normalizedStandardDeviation = new Dictionary<string, double>();

			var morrisExperimentResults = new Dictionary<string, object>() {
				{"normalized_absolute_mean", normalizedAbsoluteMean},
				{"normalized_mean", normalizedMean},
				{"normalized_absolute_standard_deviation", normalizedAbsoluteStandardDeviation},
				{"normalized_standard_deviation", normalizedStandardDeviation}
			};

			foreach (SensitivityValue sv in result.NormalizedAbsoluteMean) {
				string paramId = paramIds[sv.ParameterId];
				double value = sv.Value;
				normalizedAbsoluteMean.Add(paramId, value);
			}

			foreach (SensitivityValue sv in result.NormalizedMean) {
				string paramId = paramIds[sv.ParameterId];
				double value = sv.Value;
				normalizedMean.Add(paramId, value);
			}

			foreach (SensitivityValue sv in result.NormalizedAbsoluteStandardDeviation) {
				string paramId = paramIds[sv.ParameterId];
				double value = sv.Value;
				normalizedAbsoluteStandardDeviation.Add(paramId, value);
			}

			foreach (SensitivityValue sv in result.NormalizedStandardDeviation) {
				string paramId = paramIds[sv.ParameterId];
				double value = sv.Value;
				normalizedStandardDeviation.Add(paramId, value);
			}

			return morrisExperimentResults;
		}

		public class ScalarmParameter
		{
			public string id;
			public double min;
			public double max;
			// TODO: if type != float - ignore parameter
			public string type;

			public double[] Range {
				get {
					return new double[] { min, max };
				}
			}

			public override string ToString() {
				return string.Format("Parameter id: {0}, type: {1}, min: {2}, max: {3}", id, type, min, max);
			}
		}

		// Usage: mono Program.exe -> will read config from config.json
		// Usage: mono Program.exe -config <path> -> read config from path
		// Usage: mono Program.exe -stdin -> will read config from stdin
		public static void Main(string[] args)
		{
			var startTime = DateTime.Now;

			string configText;

			if (args.Length >= 1 && args[0] == "-stdin") {
				configText = "";
				string line;
				while ((line = Console.ReadLine()) != null && line != "") {
					configText += line;
				}

				Console.WriteLine("Config read from stdin:");
				Console.WriteLine(configText);
			} else if (args.Length >= 2 && args[0] == "-config") {
				configText = System.IO.File.ReadAllText(args[1]);
				Console.WriteLine("Config read from {0}", args[1]);
			} else {
				configText = System.IO.File.ReadAllText("config.json");
				Console.WriteLine("Config read from config.json");
			}

			JObject appConfig = JObject.Parse(configText);

			// Scalarm and Morris Design config
			string experimentManagerAddress = appConfig["address"].ToObject<string>();
			string experimentManagerUrl = String.Format("https://{0}", experimentManagerAddress);

			var parameters = appConfig["parameters"].ToObject<ScalarmParameter[]>();
			// read from first simulation result after fetching results
			// var outputIds = appConfig["output_ids"].ToObject<string[]>();
			int morrisSamplesCount = appConfig["morris_samples_count"].ToObject<int>();
			int morrisLevelsCount = appConfig["morris_levels_count"].ToObject<int>();

			var simulationIdJson = appConfig["simulation_id"];
			var simulationId = (simulationIdJson != null) ? simulationIdJson.ToObject<string>() : null;
			// ---

			// only for testing!
			var isFakeExperiment = (appConfig["fake_experiment"] != null ? appConfig["fake_experiment"].ToObject<bool>() : false);

			Scalarm.ISupervisedExperiment experiment = null;

			if (isFakeExperiment) {
				experiment = new Scalarm.FakeSupervisedExperiment();
			} else {
				// create Scalarm Client basing on credentials from script config
				Scalarm.Client client = null;
				if (appConfig["experiment_manager_proxy_path"] != null) {
					string experimentManagerProxyPath = appConfig["experiment_manager_proxy_path"].ToObject<string>();
					client = new Scalarm.ProxyCertClient(experimentManagerUrl, new FileStream(experimentManagerProxyPath, FileMode.Open));
				} else {
					string experimentManagerLogin = appConfig["user"].ToObject<string>();
					string experimentManagerPassword = appConfig["password"].ToObject<string>();
					client = new Scalarm.BasicAuthClient(experimentManagerUrl, experimentManagerLogin, experimentManagerPassword);
				}
				// ---
				// use experiment or create new with simulation_id
				string experimentId = null;
				if (simulationId != null) {
					Console.WriteLine("Using simulation {0}/simulations/{1} to instantiate experiment",  experimentManagerUrl, simulationId);
					var scenario = client.GetScenarioById(simulationId);
					experiment = scenario.CreateSupervisedExperiment(null, new Dictionary<string, object> {
						{"name", String.Format("Morris_samples_{0}", morrisSamplesCount)}
					});
				} else {
					experimentId = appConfig["experiment_id"].ToObject<string>();
					experiment =
						client.GetExperimentById<Scalarm.ISupervisedExperiment>(experimentId);
				}
			}

			Console.WriteLine ("Using experiment {0}/experiments/{1}", experimentManagerUrl, experiment.Id);

			// assuming that key order is the same as in config file
			InputProperties[] properties = new InputProperties[parameters.Count()];
			{
				for (int i=0; i<parameters.Count(); ++i) {
					Console.WriteLine(parameters[i].ToString());
					properties[i] = new InputProperties(InputValuesType.Range, parameters[i].Range);
				}
			}

			MorrisDesignSettings mdSettings = new MorrisDesignSettings(properties, morrisSamplesCount, morrisLevelsCount);
			List<MorrisDesignInput> inputs = MorrisDesign.GenerateInputs(mdSettings);
			IList<Scalarm.ValuesMap> pointsToSchedule = new List<Scalarm.ValuesMap>(inputs.Count);

			foreach (MorrisDesignInput morrisPoint in inputs) {
				var pointInput = new Scalarm.ValuesMap();
				for (int i=0; i<parameters.Count(); ++i) {
					pointInput.Add(parameters[i].id, morrisPoint.Input[i]);
				}

				pointsToSchedule.Add(pointInput);
			}

			// TODO mocked
			experiment.SchedulePoints(pointsToSchedule);

//			// start HPC resources
//			// that was only for testing, resources should be added manually
//			IList<Scalarm.PrivateMachineCredentials> machinesList = client.GetPrivateMachineCredentials("jack.metal.agh.edu.pl", "scalarm");
//			Scalarm.PrivateMachineCredentials machine = machinesList[0];
//			experiment.SchedulePrivateMachineJobs(4, machine);

			// block until results are available
			// also exceptions can be thrown if there are no resources

			// TODO mocked
			while (true) {
				try {
					experiment.WaitForDone();
					break;
				} catch (Scalarm.NoActiveSimulationManagersException e) {
					Console.WriteLine("No active SimulationManagers, waiting 5 seconds to try again...");
					Thread.Sleep(5000);
				}
			}

			// TODO mocked
			IList<Scalarm.SimulationParams> scalarmResults = experiment.GetResults();

			List<MorrisDesignOutput> morrisOutputs = new List<MorrisDesignOutput>();

			string[] ids = parameters.Select(p => p.id).ToArray();
			string[] outputIds = scalarmResults.First().Output.Keys.ToArray();

			Console.WriteLine("Output ids: {0}", String.Join(", ", outputIds));

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
						
			var results = MorrisDesign.CalculateSensitivity(mdSettings, inputs, morrisOutputs);
			
			MorrisDesignSensitivityAnalysisResult result1 = results[0];

			var experimentResult = JsonConvert.SerializeObject(MorrisExperimentResults(result1, ids));

			Console.WriteLine("Experiment result:");
			Console.WriteLine(experimentResult);

			TimeSpan executionTime = (DateTime.Now - startTime);
			Console.WriteLine("Execution time: {0} seconds", executionTime.TotalSeconds);

			experiment.MarkAsComplete(JsonConvert.SerializeObject(MorrisExperimentResults(result1, ids)));
		}
	}
}
