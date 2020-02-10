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
    public class PrintingWork : IDisposable
    {
        private Boolean _disposedValue = false;

        public PrintingWork(LabelModel model, Int32 count, Int32 width, Int32 height)
        {
            PrintingLabel = model;

            Count = count;

            Width = width;
            Height = height;


            Document = new PrintDocument();
            Document.PrintPage += new PrintPageEventHandler(PrintHandler);

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
                }

                _disposedValue = true;
            }
        }

        protected virtual void PrintHandler(Object sender, PrintPageEventArgs ev)
        {
            Layout?.Print(ev);

            --Count;
            ev.HasMorePages = Count > 0;
        }

        private PrintDocument Document { get; set; }

        private Int32 Width { get; }

        private Int32 Height { get; }

        private Int32 Count { get; set; }

        private LabelModel PrintingLabel { get; }

        private PrintingLayoutBase Layout { get; set; }

        private void StartPrinting(PrintDocument document)
        {
            Layout = PrintingLayoutFactory.GetLayout(Document, PrintingLabel, Width, Height);
            document.Print();
        }

        private Single ToInches(Single millimeters)
        {
            return millimeters * 0.0393700787f;
        }
    }
}
