using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Net.Office.Excel
{
	/// <summary>
	/// Collection class for Font objects.
	/// </summary>
	public class FontCollection : ExcelCollection<Font>
	{
		internal FontCollection(Workbook wb) : base(wb)
		{
		}
	}
}
