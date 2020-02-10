using Core.Logic.Printing.Enums;
using Core.Logic.Printing.Layout;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Printing
{
    public class PrintingLayoutFactory
    {
        public static PrintingLayoutBase GetLayout(PrintDocument document, LabelModel label, Int32 width, Int32 height)
        {
            switch(LayoutLinesCount(label))
            {
                case PrintingLayoutType.OneLine:
                    return new OneLineIndexLayout(document, label, width, height);

                case PrintingLayoutType.OneLongLine:
                    return new OneLongLineIndexLayout(document, label, width, height);

                case PrintingLayoutType.TwoLines:
                    return new TwoLineIndexLayout(document, label, width, height);

                case PrintingLayoutType.ThreeLines:
                    return new ThreeLineIndexLayout(document, label, width, height);

                default:
                    return new OneLineIndexLayout(document, label, width, height);
            }
        }

        private static PrintingLayoutType LayoutLinesCount(LabelModel label)
        {
            var length = label.Customer.Length;

            if (length > TwoLineIndexLayout.MaxIndexLength) return PrintingLayoutType.ThreeLines;

            if (length > OneLongLineIndexLayout.MaxIndexLength) return PrintingLayoutType.TwoLines;

            if (length > OneLineIndexLayout.MaxIndexLength) return PrintingLayoutType.OneLongLine;

            return PrintingLayoutType.OneLine;
        }
    }
}
