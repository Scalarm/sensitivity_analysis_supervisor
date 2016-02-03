using System;
using SensitivityAnalysis;
using SensitivityAnalysis.MorrisDesign;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using SensitivityAnalysis.MethodOfSobol;

namespace sensitivity_analysis
{
	class MainClass
	{
		/// <summary>
		/// Creates Dictionary for single MoE Sobol results.
		/// Contains results of: absolute_mean, mean, absolute_standard_deviation, standard_deviation
		/// </summary>
		/// <returns>
		/// The Dictionary with single MoE Sobol results. Structure:
		/// {
		///   "parameter1": {
		///   	"sensitivity_inices": value,
		///   	"total_effect_indices": value,
		///   },
		///   "parameter2": {...}
		/// }
		/// </returns>
		/// <param name="moeResult">Sobol result for single MoE.</param>
		/// <param name="paramIds">Input parameter ids to label parameters in result Dictionary (in this example: ["parameter1", "parameter2"].</param>
		public static IDictionary<string, IDictionary<string, double>> SobolMoeResults(MethodOfSobolSensitivityAnalysisResult moeResult, string[] paramIds)
		{
			IDictionary<string, IDictionary<string, double>> sobolMoeResults = new Dictionary<string, IDictionary<string, double>>();
			// for each paramId create sub-dictionary
			for (int i=0; i<paramIds.Length; ++i) {
				var paramName = paramIds[i];
				var paramDict = SobolParameterResults(moeResult, i);
				sobolMoeResults.Add(paramName, paramDict);
			}
			return sobolMoeResults;
		}

		/// <summary>
		/// Creates Sobol results Dictionary for single parameter of simulation for particluar MoE.
		/// </summary>
		/// <returns>
		/// The Dictionary with structure:
		/// {
		///   "sensitivity_inices": value,
		///   "total_effect_indices": value,
		/// }
		/// 
		/// </returns>
		public static IDictionary<string, double> SobolParameterResults(MethodOfSobolSensitivityAnalysisResult result, int paramIndex)
		{
			var paramDict = new Dictionary<string, double>();

			// get SA value for current parameter
			double value;

			value = result.SensitivityIndices.FirstOrDefault(sv => (sv.ParameterId == paramIndex)).Value;
			paramDict.Add("sensitivity_indices", value);

			value = result.TotalEffectIndices.FirstOrDefault(sv => (sv.ParameterId == paramIndex)).Value;
			paramDict.Add("total_effect_indices", value);

			return paramDict;
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
			int sobolBaseInputsCount = appConfig["sobol_base_inputs_count"].ToObject<int>();

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
						{"name", String.Format("Sobol_base_inputs_count_{0}", sobolBaseInputsCount)}
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

			MethodOfSobolSettings saSettings = new MethodOfSobolSettings(properties, sobolBaseInputsCount);

			// TODO: Base Input? We need .Input for each type
			List<MethodOfSobolInput> inputs = MethodOfSobol.GenerateInputs(saSettings);
			IList<Scalarm.ValuesMap> pointsToSchedule = new List<Scalarm.ValuesMap>(inputs.Count);

			foreach (MethodOfSobolInput saPoint in inputs) {
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

			List<MethodOfSobolOutput> saOutputs = new List<MethodOfSobolOutput>();

			string[] ids = parameters.Select(p => p.id).ToArray();
			string[] outputIds = scalarmResults.First().Output.Keys.ToArray();

			Console.WriteLine("Output ids: {0}", String.Join(", ", outputIds));

			foreach (MethodOfSobolInput morrisPoint in inputs) {
				// find Scalar results for morrisPoint
				// TODO: optimize: store flatten Scalarm input in dictionary
				var res = scalarmResults.Where(r => Enumerable.SequenceEqual(r.Input.Flatten(ids).Select(x => Convert.ToDouble(x)), morrisPoint.Input));

				if (res.Any()) {
					// TODO: handle simulation error - do not add these output values

					// Scalarm output dictionary
					var output = res.First().Output;

					// flatten results with conversion to double
					List<Double> resultValues = new List<Double>(output.Flatten(outputIds).Select(x => Convert.ToDouble(x)));

					saOutputs.Add(new MethodOfSobolOutput(morrisPoint.InputId, resultValues));
				} else {
					Console.WriteLine(String.Format("Result not found for {0}", morrisPoint.InputId.InputId));
				}
			}

			var notCalculated = BaseSa.GetNotCalculatedInputs<MethodOfSobolInput, MethodOfSobolOutput>(inputs, saOutputs);
						
			var results = MethodOfSobol.CalculateSensitivity(saSettings, inputs, saOutputs);

			// TODO: SCAL-999 - support for multiple moes

			// -- format results to send them to Scalarm

			// for each MoE generate sensitivity analysis results
			var moesDict = new Dictionary<string, IDictionary<string, IDictionary<string, double>>>();
			for (int i = 0; i < outputIds.Length; ++i) {
				MethodOfSobolSensitivityAnalysisResult result = results[i];
				string moeName = outputIds[i];
				Console.WriteLine("Formatting results for moe {0}...", moeName);
				var moeResult = SobolMoeResults(result, ids);

				// add MoE results to MoEs results dict
				moesDict.Add(moeName, moeResult);
			}

			// this will be a Dictionary finally converted to experiment results JSON
			var sobolExperimentResults = new Dictionary<string, object>() {
				{"sensitivity_analysis_method", "sobol"},
				{"moes", moesDict}
			};

			TimeSpan executionTime = (DateTime.Now - startTime);
			Console.WriteLine("Execution time: {0} seconds", executionTime.TotalSeconds);

			var resultsJson = JsonConvert.SerializeObject(sobolExperimentResults);
			Console.WriteLine("SA results:\n" + resultsJson);

			experiment.MarkAsComplete(resultsJson);
		}
	}
}
