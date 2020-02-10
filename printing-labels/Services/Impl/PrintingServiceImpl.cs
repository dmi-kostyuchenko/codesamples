using Core.Data;
using Core.Data.Base;
using Core.Data.Entities;
using Core.Data.Model;
using Core.Logging;
using Core.Logic.Printing;
using Core.Logic.Printing.Layout;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Services.Impl
{
    public class PrintingServiceImpl : IPrintingService
    {
        public PrintingServiceImpl()
        {
        }

        public Int32 PageWidth { get; private set; }

        public Int32 PageHeight { get; private set; }

        public Int32 MaxPagesPerDocument { get; private set; }

        /// <summary>
        /// Sets printing default values for further use
        /// </summary>
        /// <param name="width">The label width</param>
        /// <param name="height">The label height</param>
        /// <param name="maxPagesPerDocument">The max labels to print in a single document</param>
        public void SetPrintingDefaults(Int32 width, Int32 height, Int32 maxPagesPerDocument)
        {
            PageWidth = width;
            PageHeight = height;
            MaxPagesPerDocument = maxPagesPerDocument;
        }

        /// <summary>
        /// Prints the label
        /// </summary>
        /// <param name="label">The label entity to print</param>
        /// <param name="isPrintDialogRequired">Flag describing when the printer selection and configuration dialog is required</param>
        public void PrintLabel(LabelEntity label, Boolean isPrintDialogRequired)
        {
            using (var work = new PrintingWork(new LabelModel
            {
                UniqueIdentifier = label.UniqueID,
                Customer = label.Customer,
                Date = label.CreateDate
            }, label.ItemCount, 40, 40)) //TODO: replace with values from the configuration
            {
                work.Print(isPrintDialogRequired);
            }
        }

        /// <summary>
        /// Prints serie of labels
        /// </summary>
        /// <param name="labels">The labels list</param>
        /// <param name="itemCount">The number of items of each label</param>
        /// <param name="isPrintDialogRequired">Flag describing when the printer selection and configuration dialog is required</param>
        public void PrintSerie(IEnumerable<LabelEntity> labels, Int32 itemCount, Boolean isPrintDialogRequired)
        {
            var models = new List<LabelModel>(labels.Count() * itemCount);
            foreach(var label in labels)
            {
                for(var i = 0; i < label.ItemCount; ++i)
                {
                    models.Add(new LabelModel
                    {
                        UniqueIdentifier = label.UniqueID,
                        Customer = label.Customer,
                        Date = label.CreateDate
                    });
                }
            }

            using (var work = new PrintingWorkSerie(models, MaxPagesPerDocument , PageWidth, PageHeight))
            {
                work.Print(isPrintDialogRequired);
            }
        }
    }
}
