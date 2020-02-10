using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Printing.Layout
{
    public class ThreeLineIndexLayout : CustomLabelLayout
    {
        public ThreeLineIndexLayout(PrintDocument document, LabelModel label, Int32 width, Int32 height) : base(document, label, width, height)
        {

        }

        protected override Single IdentifierZoneHeight => 0.12f;

        protected override Single IdentifierFontSize => 0.12f;

        protected override Single QRCodeZoneHeight => 0.5f;

        protected override Single IndexZoneHeight => 0.3f;

        protected override Single IndexFontSize => 0.09f;

        protected override Single DateZoneHeight => 0.08f;

        protected override Single DateFontSize => 0.08f;
    }
}
