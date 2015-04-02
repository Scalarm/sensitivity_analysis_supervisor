using System;
using SensitivityAnalysis;
using SensitivityAnalysis.MorrisDesign;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

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
		private static double[] testFunction(double[] x)
		{
			double y = 5 * x [0]
				+ x [1] * x [1]
				+ 10 * x [2];

			return new double[] { y };
		}

		public static Config ReadConfigFile(string path)
		{
			string configText = System.IO.File.ReadAllText(path);
			return JsonConvert.DeserializeObject<Config>(configText);
		}

		public static void Main(string[] args)
		{
			// TODO: allow read config from stdin
			Config appConfig = ReadConfigFile("config.json");

			// TODO: LOAD CONFIG
			var experimentId = appConfig.experiment_id;
			var experimentManagerUrl = appConfig.experiment_manager_url;
			var morrisSamplesCount = appConfig.morris_samples_count;
			var morrisLevelsCount = appConfig.morris_level_count;

			Scalarm.Client client = null;
			if (!String.IsNullOrEmpty(appConfig.experiment_manager_proxy_path)) {
				client = new Scalarm.ProxyCertClient(experimentManagerUrl, new FileStream(appConfig.experiment_manager_proxy_path, FileMode.Open));
			} else {
				// TODO: support for experiment manager login/password
				throw new NotImplementedException("Experiment Manager login password auth");
			}

			Scalarm.SupervisedExperiment experiment = client.GetExperimentById<Scalarm.SupervisedExperiment>(experimentId);

			// generate inputs
			// TODO: get parameters ids and ranges
			// assume:

			// TODO: assuming that key order is the same as in config file
			Dictionary<string, Tuple<double, double>> parameters = appConfig.parameters;
			InputProperties[] properties = new InputProperties[parameters.Count];
			int i = 0;
			foreach (KeyValuePair<string, double[]> param in parameters) {
				properties[i++] = new InputProperties(InputValuesType.Range, param.Value);
			}
//			properties[0] = new InputProperties(InputValuesType.Range, new double[] {0, 100});
//			properties[1] = new InputProperties(InputValuesType.Range, new double[] {-100, 100});
//			properties[2] = new InputProperties(InputValuesType.Range, new double[] {0, 100});
			List<MorrisDesignInput> inputs = MorrisDesignCore.GenerateInputs(properties, morrisSamplesCount, morrisLevelsCount);

			// TODO: add points
			foreach (MorrisDesignInput morrisPoint in inputs) {
				var x = morrisPoint.Input[0];
				var y = morrisPoint.Input[1];
				var z = morrisPoint.Input[2];
				experiment.SchedulePoint(new Scalarm.ValuesMap {
					{"x", x},
					{"y", y},
					{"z", z}
				});
			}

			// start HPC resources
			// that was only for testing, resources should be added manually
			//IList<Scalarm.PrivateMachineCredentials> machinesList = client.GetPrivateMachineCredentials("jack.metal.agh.edu.pl", "scalarm");
			//Scalarm.PrivateMachineCredentials machine = machinesList[0];
			// experiment.SchedulePrivateMachineJobs(1, machine);

			experiment.WaitForDone();

			IList<Scalarm.SimulationParams> scalarmResults = experiment.GetResults();

			List<MorrisDesignOutput> outputs = new List<MorrisDesignOutput>();

			foreach (MorrisDesignInput input in inputs) {
				var x = input.Input[0];
				var y = input.Input[1];
				var z = input.Input[2];

				// search for point sample in Scalarm-fetched results
				var res = scalarmResults.Where(r => (Convert.ToDouble(r.Input["x"]) == x) && (Convert.ToDouble(r.Input["y"]) == y) && (Convert.ToDouble(r.Input["z"]) == z));

				if (res.Any()) {
					// TODO: handle simulation error - do not add these output values

					var output = res.First().Output;
					// flatten Scalarm Output (create properly-ordered results list)
					var morrisRes = new List<Double>() {
						// TODO: specific for executor/output_reader
						Convert.ToDouble(output["result"])
					};
					outputs.Add(new MorrisDesignOutput(input.InputId, morrisRes));
				} else {
					Console.WriteLine(String.Format("Result not found for {0}", input.InputId.InputId));
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
