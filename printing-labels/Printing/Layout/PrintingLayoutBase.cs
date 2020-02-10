using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Printing.Layout
{
    public abstract class PrintingLayoutBase
    {
        public PrintingLayoutBase(PrintDocument document)
        {
            Document = document;
        }

        protected PrintDocument Document { get; set; }

        protected Int32 ElementsCount { get; set; }

        protected Int32 TotalOffset
        {
            get
            {
                return ElementsCount > 0 ? ElementsCount - 1 : 0;
            }
        }

        protected Single XScale
        {
            get
            {
                return Document.DefaultPageSettings.PrinterResolution.X / 100;
            }
        }

        protected Single YScale
        {
            get
            {
                return Document.DefaultPageSettings.PrinterResolution.Y / 100;
            }
        }

        protected Int32 Width
        {
            get
            {
                return Document.DefaultPageSettings.PaperSize.Width;
                       //- Document.DefaultPageSettings.Margins.Left 
                       //- Document.DefaultPageSettings.Margins.Right;
            }
        }

        protected Int32 Height
        {
            get
            {
                return Document.DefaultPageSettings.PaperSize.Height - TotalOffset;
                       //- Document.DefaultPageSettings.Margins.Top
                       //- Document.DefaultPageSettings.Margins.Bottom;
            }
        }

        public virtual void Print(PrintPageEventArgs ev)
        {
        }

        protected Int32 ScaleX(Int32 x)
        {
            return x;// Convert.ToInt32(x * XScale);
        }

        protected Int32 ScaleY(Int32 y)
        {
            return y;// Convert.ToInt32(y * YScale);
        }

        protected Single ScaleX(Single x)
        {
            return x;// x * XScale;
        }

        protected Single ScaleY(Single y)
        {
            return y;// y * YScale;
        }
    }
}
