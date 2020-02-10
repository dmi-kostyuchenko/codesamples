using ClosedXML.Excel;
using Core.Data;
using Core.Data.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Services
{
    public interface IReportGeneratingService
    {
        /// <summary>
        /// Marks all labels found in dbo.isCUSTOMER table with status = 1 and generates report with not found ones
        /// </summary>
        /// <param name="connectionModel1">The labels DB connection model</param>
        /// <param name="connectionModel2">The customer DB connection model</param>
        /// <param name="date">The current date</param>
        XLWorkbook MarkLabelsProcessedAndGenerateReport(DatabaseConnectionModel connectionModel1, DatabaseConnectionModel connectionModel2, LabelsGeneratorDbTables tables, 
                                                        DateTime date, out Boolean isReportContainData);
    }
}
