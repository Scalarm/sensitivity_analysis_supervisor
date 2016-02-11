using System;
using SensitivityAnalysis;
using SensitivityAnalysis.MethodOfSobol;
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
		public static Dictionary<string, IDictionary<string, IDictionary<string, double>>> CreateMoesDict<T>(T[] results, string[] parameterIds, String[] outputIds) {
			// TODO: make a function with generics (?) where results is of type Array<T>
			// for each MoE generate sensitivity analysis results
			var moesDict = new Dictionary<string, IDictionary<string, IDictionary<string, double>>>();
			for (int i = 0; i < outputIds.Length; ++i) {
				T result = results[i];
				string moeName = outputIds[i];
				Console.WriteLine("Formatting results for moe {0}...", moeName);
				IResultsAnalyzer<T> analyzer = (IResultsAnalyzer<T>)ResultsAnalyzerFactory.GetResultsAnalyzer(results.GetType());
				var moeResult = analyzer.MoeResults(result, parameterIds);

				// add MoE results to MoEs results dict
				moesDict.Add(moeName, moeResult);
			}
			return moesDict;
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

			var simulationIdJson = appConfig["simulation_id"];
			var simulationId = (simulationIdJson != null) ? simulationIdJson.ToObject<string>() : null;

			string methodType = appConfig["method_type"].ToObject<string>();
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
						{"name", String.Format("CSharp SA {0}", methodType)}
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

			// TODO remove this
			BaseSettings saSettings = null;
			BaseSa saMethod = null;
			BaseInputs inputs = null;

			switch (methodType)
			{
			case "sobol":
				int sobolBaseInputsCount = appConfig["sobol_base_inputs_count"].ToObject<int>();
				Console.WriteLine("Using Sobol method with config:");
				Console.WriteLine("- sobol_base_inputs_count: " + sobolBaseInputsCount);

				saSettings = new MethodOfSobolSettings(properties, sobolBaseInputsCount);
				saMethod = new MethodOfSobol();
				break;
			case "morris":
				int morrisSamplesCount = appConfig["morris_samples_count"].ToObject<int>();
				int morrisLevelsCount = appConfig["morris_levels_count"].ToObject<int>();
				Console.WriteLine("Using Sobol method with config:");
				Console.WriteLine("- morris_samples_count: " + morrisSamplesCount);
				Console.WriteLine("- morris_levels_count: " + morrisLevelsCount);

				saSettings = new MorrisDesignSettings(properties, morrisSamplesCount, morrisLevelsCount);
				saMethod = new MorrisDesign();

				break;
			default:
				// TODO: use experiment end with error reason
				var errorMsg = "FATAL: method type not supported: '" + methodType + "'";
				Console.WriteLine(errorMsg);
				throw new Exception(errorMsg);
			}

			saMethod.GenerateInputs(saSettings);
			inputs = saMethod.Inputs;

			IList<Scalarm.ValuesMap> pointsToSchedule = new List<Scalarm.ValuesMap>(inputs.Inputs.Count);

			foreach (BaseInput saPoint in inputs.Inputs) {
				var pointInput = new Scalarm.ValuesMap();
				for (int i=0; i<parameters.Count(); ++i) {
					pointInput.Add(parameters[i].id, saPoint.Input[i]);
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

			string[] ids = parameters.Select(p => p.id).ToArray();
			string[] outputIds = scalarmResults.First().Output.Keys.ToArray();

			Console.WriteLine("Output ids: {0}", String.Join(", ", outputIds));

			foreach (BaseInput saPoint in inputs.Inputs) {
				// find Scalar results for morrisPoint
				// TODO: optimize: store flatten Scalarm input in dictionary
				var res = scalarmResults.Where(r => Enumerable.SequenceEqual(r.Input.Flatten(ids).Select(x => Convert.ToDouble(x)), saPoint.Input));

				if (res.Any()) {
					// TODO: handle simulation error - do not add these output values

					// Scalarm output dictionary
					var output = res.First().Output;

					// flatten results with conversion to double
					List<Double> resultValues = new List<Double>(output.Flatten(outputIds).Select(x => Convert.ToDouble(x)));

					saMethod.Outputs.Add(saPoint.InputId, resultValues);
				} else {
					Console.WriteLine(String.Format("Result not found for {0}", saPoint.InputId));
				}
			}

			// TODO: use "not calculated" (method)

			// this will be a Dictionary finally converted to experiment results JSON
			Dictionary<string, object> experimentResults = null;

			string resultsJson = null;

			var moesDict = new Dictionary<string, IDictionary<string, IDictionary<string, double>>>();

			switch (methodType)
			{
			case "morris":
				{
					MorrisDesignSensitivityAnalysisResult[] results =
						((MorrisDesign)saMethod).CalculateSensitivity(saSettings);

					moesDict = CreateMoesDict<MorrisDesignSensitivityAnalysisResult>(results, ids, outputIds);
				}
				break;
			case "sobol":
				{
					MethodOfSobolSensitivityAnalysisResult[] results =
						((MethodOfSobol)saMethod).CalculateSensitivity(saSettings);

					moesDict = CreateMoesDict<MethodOfSobolSensitivityAnalysisResult>(results, ids, outputIds);
				}
				break;
			}

			experimentResults = new Dictionary<string, object>() {
				{"sensitivity_analysis_method", methodType},
				{"moes", moesDict}
			};

			// -- format results to send them to Scalarm	

			TimeSpan executionTime = (DateTime.Now - startTime);
			Console.WriteLine("Execution time: {0} seconds", executionTime.TotalSeconds);

			resultsJson = JsonConvert.SerializeObject(experimentResults);
			Console.WriteLine("SA results:\n" + resultsJson);

			experiment.MarkAsComplete(resultsJson);
		}
	}
}
