using QueryMutatorv2; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TESTER
{
    public class Kutya
    {
        public string name;
        public int labak;
    }
    public class Cica
    {
        public string name;
        public int labak;
    }
    class Program
    {
        static void Main(string[] args)
        {
            
            var kutya=new Kutya { name="asdsadasd",labak=5};
            var a= kutya.Map() ;
            Cica c = a.To<Cica>();
            Console.WriteLine(c.labak);
        }
    }
}
