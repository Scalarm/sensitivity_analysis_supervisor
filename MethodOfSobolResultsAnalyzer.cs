using System;
using SensitivityAnalysis.MethodOfSobol;
using System.Collections.Generic;
using System.Linq;

namespace sensitivity_analysis
{
	public class MethodOfSobolResultsAnalyzer : IResultsAnalyzer<MethodOfSobolSensitivityAnalysisResult>
	{
		public MethodOfSobolResultsAnalyzer()
		{
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

		#region IResultsAnalyser implementation
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
		public IDictionary<string, System.Collections.Generic.IDictionary<string, double>> MoeResults(MethodOfSobolSensitivityAnalysisResult moeResult, string[] paramIds)
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
		#endregion
	}
}

