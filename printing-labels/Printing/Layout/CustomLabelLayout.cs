using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Logic.Printing.Layout
{
    public abstract class CustomLabelLayout : PrintingLayoutBase
    {
        private static QRCodeGenerator _qrGenerator = new QRCodeGenerator();

        public static Int32 MaxIndexLength => 0;

        public CustomLabelLayout(PrintDocument document, LabelModel label, Int32 width, Int32 height) : base(document)
        {
            Label = label;
            ElementsCount = 4;
        }

        public override void Print(PrintPageEventArgs ev)
        {
            PrintQrCode(ev.Graphics);

            PrintIdentifier(ev.Graphics);

            PrintIndex(ev.Graphics);

            PrintDate(ev.Graphics);
        }

        protected virtual Single IdentifierZoneHeight => 0f;

        protected virtual Single IdentifierFontSize => 0f;

        protected virtual Single QRCodeZoneHeight => 0f;

        protected virtual Single IndexZoneHeight => 0f;

        protected virtual Single IndexFontSize => 0f;

        protected virtual Single DateZoneHeight => 0f;

        protected virtual Single DateFontSize => 0f;

        private LabelModel Label { get; }

        private void PrintIdentifier(Graphics g)
        {
            var height = IdentifierZoneHeight * Height;
            var fontSize = IdentifierFontSize * Height; 

            var sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;

            var rect = new RectangleF(0, 0, ScaleX(Width), ScaleY(height));
            var font = new Font("Arial", ScaleY(fontSize), FontStyle.Bold, GraphicsUnit.Pixel);

            g.DrawString(Label.UniqueIdentifier, font, Brushes.Black, rect, sf);
        }

        private void PrintQrCode(Graphics g)
        {
            var ypos = IdentifierZoneHeight * Height + 1;
            var height = QRCodeZoneHeight * Height;

            using (var qrGenerator = new QRCodeGenerator())
            {
                using (var qrCodeData = qrGenerator.CreateQrCode(Label.UniqueIdentifier, QRCodeGenerator.ECCLevel.H))
                using (var qrCode = new QRCode(qrCodeData))
                using (var qrCodeImage = qrCode.GetGraphic(Convert.ToInt32(ScaleY(height)), Color.Black, Color.White, false))
                {
                    var xpos = (Width - height) / 2;
                    var rect = new RectangleF(xpos, ScaleY(ypos), ScaleX(height), ScaleY(height));
                    g.DrawImage(qrCodeImage, rect);
                    Thread.Sleep(1);
                }
            }
        }

        private void PrintIndex(Graphics g)
        {
            var ypos = IdentifierZoneHeight * Height + 1 + QRCodeZoneHeight * Height + 1;
            var height = IndexZoneHeight * Height;
            var fontSize = IndexFontSize * Height;

            var sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            var rect = new RectangleF(0, ScaleY(ypos), ScaleX(Width), ScaleY(height));
            var font = new Font("Arial", ScaleY(fontSize), GraphicsUnit.Pixel);

            g.DrawString(Label.Customer, font, Brushes.Black, rect, sf);
        }

        private void PrintDate(Graphics g)
        {
            var ypos = IdentifierZoneHeight * Height + 1 + QRCodeZoneHeight * Height + 1 + IndexZoneHeight * Height + 1;
            var height = DateZoneHeight * Height;
            var fontSize = DateFontSize * Height;

            var sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            var rect = new RectangleF(0, ScaleY(ypos), ScaleX(Width), ScaleY(height));
            var font = new Font("Arial", ScaleY(fontSize), GraphicsUnit.Pixel);

            g.DrawString(Label.DateString, font, Brushes.Black, rect, sf);
        }
    }
}
