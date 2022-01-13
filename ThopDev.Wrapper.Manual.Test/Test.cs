//using ThopDev.AutoWrapper;

using ThopDev.AutoWrapper;

namespace ThopDev.Wrapper.Manual.Test
{
    [Wrap(typeof(ToWrap))]
    public partial class Test : ITest
    {
        
    }

    public class ToWrap
    {
        public void TestFunction(string ab, bool test, int number)
        {

        }
    }

    public class X
    {
        public X()
        {
            var x = new Test(new ToWrap()) as ITest;
            x.TestFunction("ab", true, 5);
        }
    }
}
