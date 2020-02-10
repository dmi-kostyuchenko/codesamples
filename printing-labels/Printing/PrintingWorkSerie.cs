using Core.Data.Entities;
using Core.Logging;
using Core.Logic.Printing.Layout;
using Core.Logic.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Core.Logic.Printing
{
    public class PrintingWorkSerie : IDisposable
    {
        private Boolean _disposedValue = false;

        public PrintingWorkSerie(IEnumerable<LabelModel> models, Int32 maxPagesPerDocument, Int32 width, Int32 height)
        {
            PrintingLabels = models;

            Width = width;
            Height = height;

            MaxPagesPerDocument = maxPagesPerDocument;

            PrintedDocuments = new List<PrintDocument>();

            Document = new PrintDocument();
            Document.PrintPage += new PrintPageEventHandler(PrintHandler);
            Document.EndPrint += new PrintEventHandler(EndPrintHandler);

            Document.DefaultPageSettings.PaperSize = new PaperSize("Custom Label", Convert.ToInt32(ToInches(width) * 100),
                                                                                   Convert.ToInt32(ToInches(height) * 100));
            Document.DefaultPageSettings.Margins = new Margins(1, 1, 1, 1);
            Document.DefaultPageSettings.Color = false;

            Document.DefaultPageSettings.PrinterResolution = new PrinterResolution
            {
                Kind = PrinterResolutionKind.Custom,
                X = 300,
                Y = 300
            };
        }

        public void Print(Boolean isPrintDialogRequired)
        {
            if (isPrintDialogRequired)
            {
                using (var dialog = new PrintDialog())
                {
                    dialog.Document = Document;
                    var result = dialog.ShowDialog();
                    if (result == DialogResult.OK)
                    {
                        StartPrinting(dialog.Document);
                    }
                }
            }
            else
            {
                StartPrinting(Document);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Document.Dispose();
                    PrintedDocuments.ForEach(x => x.Dispose());
                }

                _disposedValue = true;
            }
        }

        protected virtual void PrintHandler(Object sender, PrintPageEventArgs ev)
        {
            var layout = PrintingLayoutFactory.GetLayout(Document, PrintingLabels.ElementAt(Index), Width, Height);
            layout.Print(ev);

            ++Index;
            ++Count;
            ev.HasMorePages = Index < PrintingLabels.Count() && Count < MaxPagesPerDocument;
        }

        protected virtual void EndPrintHandler(Object sender, PrintEventArgs ev)
        {
            if (Index < PrintingLabels.Count() && Count >= MaxPagesPerDocument)
            {
                Count = 0;

                var prevDocument = Document;

                Document = new PrintDocument();
                Document.PrintPage += new PrintPageEventHandler(PrintHandler);
                Document.EndPrint += new PrintEventHandler(EndPrintHandler);

                Document.DefaultPageSettings = prevDocument.DefaultPageSettings;
                Document.PrinterSettings = prevDocument.PrinterSettings;

                Document.Print();

                prevDocument.Dispose();
            }
        }

        private PrintDocument Document { get; set; }

        private List<PrintDocument> PrintedDocuments { get; }

        private Int32 Width { get; }

        private Int32 Height { get; }

        private Int32 MaxPagesPerDocument { get; }

        private Int32 Index { get; set; }

        private Int32 Count { get; set; }

        private IEnumerable<LabelModel> PrintingLabels { get; }

        private IEnumerable<PrintingLayoutBase> Layouts { get; set; }

        private void StartPrinting(PrintDocument document)
        {
            Index = 0;
            Count = 0;
            document.Print();
        }

        private Single ToInches(Single millimeters)
        {
            return millimeters * 0.0393700787f;
        }
    }
}
