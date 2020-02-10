using Core.Logic.Printing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Services.Impl
{
    public class IdentifiersGeneratingServiceImpl : IIdentifiersGeneratingService
    {
        /// <summary>
        /// Generates the next identifier of label
        /// </summary>
        /// <param name="prevId">The previous identifier</param>
        /// <returns></returns>
        public String NextIdentifier(String prevId)
        {
            prevId = prevId.ToUpper();

            var letter = !String.IsNullOrEmpty(prevId) ? prevId[0] : Char.MinValue;
            var number = prevId.Length > 1 ? prevId.Substring(1) : String.Empty;

            number = IncrementNumber(number, out Boolean isLetterIncrement);
            if(isLetterIncrement)
            {
                letter = IncrementLetter(letter);
            }

            return $"{letter}{number}";
        }

        private String IncrementNumber(String number, out Boolean isLetterIncrementRequired)
        {
            if (Int32.TryParse(number, out Int32 num))
            {
                ++num;
                isLetterIncrementRequired = num >= 10000;
                if (isLetterIncrementRequired) num = 1;
                number = num.ToString("0000");
            }
            else
            {
                isLetterIncrementRequired = true;
                number = "0000"; //start with A0000, B0000, C0000, etc.
            }

            return number;
        }

        private Char IncrementLetter(Char letter)
        {
            if (Char.IsLetter(letter))
            {
                var letterNum = (Int32)letter;
                ++letterNum;

                letterNum = letterNum > 90 ? 65 : letterNum;
                letter = (Char)letterNum;
            }
            else
            {
                letter = 'A';
            }

            return letter;
        }
    }
}
