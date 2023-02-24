using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sake
{
    public static class Sample
    {
        static Sample()
        {
            var path = "Sample.txt";
            
            Amounts = File.ReadAllLines(path).Select(x => decimal.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        }

        public static decimal[] Amounts { get; }
    }
}
