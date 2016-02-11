using System;
using SensitivityAnalysis.MethodOfSobol;
using SensitivityAnalysis.MorrisDesign;

namespace sensitivity_analysis
{
	public class ResultsAnalyzerFactory
	{
		public ResultsAnalyzerFactory()
		{
		}

		// TODO: extremely fragile! use regexp for typename resolve
		public static Object GetResultsAnalyzer(Type methodType)
		{
			var methodTypeName = methodType.ToString();
			switch (methodTypeName)
			{
			case "SensitivityAnalysis.MethodOfSobol.MethodOfSobolSensitivityAnalysisResult[]":
				return new MethodOfSobolResultsAnalyzer();
			case "SensitivityAnalysis.MorrisDesign.MorrisDesignSensitivityAnalysisResult[]":
				return new MorrisDesignResultsAnalyzer();
			default:
				throw new Exception("Method type unknown: " + methodType);
			}
		}
	}
}

