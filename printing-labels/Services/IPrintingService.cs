using Core.Data;
using Core.Data.Entities;
using Core.Data.Model;
using Core.Logic.Printing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Services
{
    public interface IPrintingService
    {
        /// <summary>
        /// Prints the label
        /// </summary>
        /// <param name="label">The label entity to print</param>
        /// <param name="isPrintDialogRequired">Flag describing when the printer selection and configuration dialog is required</param>
        void PrintLabel(LabelEntity label, Boolean isPrintDialogRequired);

        /// <summary>
        /// Sets printing default values for further use
        /// </summary>
        /// <param name="width">The label width</param>
        /// <param name="height">The label height</param>
        /// <param name="maxPagesPerDocument">The max labels to print in a single document</param>
        void SetPrintingDefaults(Int32 width, Int32 height, Int32 maxPagesPerDocument);

        /// <summary>
        /// Prints serie of labels
        /// </summary>
        /// <param name="labels">The labels list</param>
        /// <param name="itemCount">The number of items of each label</param>
        /// <param name="isPrintDialogRequired">Flag describing when the printer selection and configuration dialog is required</param>
        void PrintSerie(IEnumerable<LabelEntity> labels, Int32 itemCount, Boolean isPrintDialogRequired);
    }
}
