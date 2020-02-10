using Core.Logic.Printing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Services
{
    public interface IIdentifiersGeneratingService
    {
        /// <summary>
        /// Generates the next identifier of label
        /// </summary>
        /// <param name="prevId">The previous identifier</param>
        /// <returns></returns>
        String NextIdentifier(String prevId);
    }
}
