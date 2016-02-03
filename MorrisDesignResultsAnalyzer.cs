using System;
using System.Collections.Generic;
using System.Linq;
using SensitivityAnalysis.MorrisDesign;

namespace sensitivity_analysis
{
	public class MorrisDesignResultsAnalyzer : IResultsAnalyzer<MorrisDesignSensitivityAnalysisResult>
	{
		public MorrisDesignResultsAnalyzer()
		{
		}

		public static IDictionary<string, double> MorrisParameterResults(MorrisDesignSensitivityAnalysisResult result, int paramIndex)
		{
			var paramDict = new Dictionary<string, double>();

			// get SA value for current parameter
			double value;

			value = result.NormalizedAbsoluteMean.FirstOrDefault(sv => (sv.ParameterId == paramIndex)).Value;
			paramDict.Add("normalized_absolute_mean", value);

			value = result.NormalizedMean.FirstOrDefault(sv => (sv.ParameterId == paramIndex)).Value;
			paramDict.Add("normalized_mean", value);

			value = result.NormalizedAbsoluteStandardDeviation.FirstOrDefault(sv => (sv.ParameterId == paramIndex)).Value;
			paramDict.Add("normalized_absolute_standard_deviation", value);

			value = result.NormalizedStandardDeviation.FirstOrDefault(sv => (sv.ParameterId == paramIndex)).Value;
			paramDict.Add("normalized_standard_deviation", value);

			return paramDict;
		}

		// TODO: this can be refactored IResultsAnalyzer -> AbstractResultsAnalyzer, where MoeResults can be its method using abstract method *ParameterResults

		#region IResultsAnalyser implementation
		public IDictionary<string, System.Collections.Generic.IDictionary<string, double>> MoeResults(MorrisDesignSensitivityAnalysisResult moeResult, string[] paramIds)
		{
			IDictionary<string, IDictionary<string, double>> sobolMoeResults = new Dictionary<string, IDictionary<string, double>>();
			// for each paramId create sub-dictionary
			for (int i=0; i<paramIds.Length; ++i) {
				var paramName = paramIds[i];
				var paramDict = MorrisParameterResults(moeResult, i);
				sobolMoeResults.Add(paramName, paramDict);
			}
			return sobolMoeResults;
		}
		#endregion
	}
}

