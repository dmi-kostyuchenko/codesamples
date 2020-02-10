using ClosedXML.Excel;
using Core.Data;
using Core.Data.Base;
using Core.Data.Entities;
using Core.Data.Model;
using Core.Logging;
using Core.Logic.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Services.Impl
{
    public class ReportGeneratingServiceImpl : IReportGeneratingService
    {
        private static readonly String[] _reportCellTitles = new String[] { "Uniue Identifier", "Index", "Date", "Status" };
        private static readonly Int32[] _reportCellWidths = new Int32[] { 15, 20, 16, 7 };

        private readonly IContextFactory _contextFactory;
        private readonly ILogger _logger;

        public ReportGeneratingServiceImpl(IContextFactory contextFactory, ILogger logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Marks all labels found in dbo.isCUSTOMER table with status = 1 and generates report with not found ones
        /// </summary>
        /// <param name="connectionModel1">The labels DB connection model</param>
        /// <param name="connectionModel2">The customer DB connection model</param>
        /// <param name="date">The current date</param>
        public XLWorkbook MarkLabelsProcessedAndGenerateReport(DatabaseConnectionModel connectionModel1,
                                                               DatabaseConnectionModel connectionModel2,
                                                               LabelsGeneratorDbTables tables, 
                                                               DateTime date,
                                                               out Boolean isReportContainData)
        {
            var labels = MarkLabelsAsProcessedAndGetMissing(connectionModel1, connectionModel2, tables, date);
            return GenerateReport(labels, out isReportContainData);
        }

        private IEnumerable<LabelEntity> MarkLabelsAsProcessedAndGetMissing(DatabaseConnectionModel connectionModel1,
                                                                            DatabaseConnectionModel connectionModel2,
                                                                            LabelsGeneratorDbTables tables,
                                                                            DateTime date)
        {
            using (var context1 = _contextFactory.GetContext(connectionModel1, tables))
            using (var context2 = _contextFactory.GetContext(connectionModel2, tables))
            {
                var reportId = Guid.NewGuid();

                var startDate = date.StartOfDay();
                var endDate = date.EndOfDayForDatabase();

                var labels = context1.Set<LabelEntity>().Where(x => x.Status == 0 && x.CreateDate >= startDate && x.CreateDate <= endDate).ToArray();
                var labelIdentifiers = labels.Select(x => x.UniqueID.Trim()).ToArray();

                var identifiers = context2.Set<CustomerEntity>().Where(x => labelIdentifiers.Contains(x.UniqueID.Trim())).Select(x => x.UniqueID.Trim()).ToArray();
                var foundLabels = labels.Where(x => identifiers.Contains(x.UniqueID.Trim())).ToArray();
                foreach (var label in foundLabels)
                {
                    label.Status = 1;
                }
                context1.SaveChanges();

                var notFoundLabels = labels.Where(x => !identifiers.Contains(x.UniqueID.Trim())).ToArray();
                return notFoundLabels;
            }
        }

        private XLWorkbook GenerateReport(IEnumerable<LabelEntity> labels, out Boolean isReportContainData)
        {
            var workbook = new XLWorkbook();

            var worksheet = workbook.AddWorksheet("Report");
            worksheet.Row(1).Style.Font.Bold = true;
            for (var i = 1; i <= _reportCellTitles.Length; ++i)
            {
                worksheet.Cell(1, i).Value = _reportCellTitles[i - 1];
                worksheet.Column(i).Width = _reportCellWidths[i - 1];
            }

            isReportContainData = labels.Any();

            var row = 2;
            foreach(var label in labels.OrderBy(x => x.CreateDate))
            {
                var index = 1;

                worksheet.Cell(row, index++).Value = label.UniqueID;
                worksheet.Cell(row, index++).Value = label.Customer;
                worksheet.Cell(row, index++).Value = label.CreateDate.ToString("dd-MM-yyyy HH:mm:ss");
                worksheet.Cell(row, index++).Value = label.Status.ToString();

                ++row;
            }

            return workbook;
        }
    }
}
