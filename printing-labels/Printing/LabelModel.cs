using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Printing
{
    public class LabelModel
    {
        public String UniqueIdentifier { get; set; }

        public String Customer { get; set; }

        public DateTime Date { get; set; }

        public String DateString
        {
            get
            {
                return Date.ToString("dd-MM-yyyy");
            }
        }
    }
}
