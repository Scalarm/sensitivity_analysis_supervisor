using System;
using System.Collections.Generic;

namespace sensitivity_analysis
{
	public interface IResultsAnalyzer<T>
	{
		IDictionary<string, IDictionary<string, double>> MoeResults(T moeResult, string[] paramIds);
	}
}

