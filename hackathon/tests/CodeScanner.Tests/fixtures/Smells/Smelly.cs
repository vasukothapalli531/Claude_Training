namespace Fixtures;

public class Smelly
{
    public void TooManyParams(int a, int b, int c, int d, int e, int f, int g)
    {
        // 7 params -> long_parameter_list (low)
    }

    public void DeeplyNested()
    {
        if (true)
        {
            for (var i = 0; i < 10; i++)
            {
                while (i > 0)
                {
                    if (i % 2 == 0)
                    {
                        if (i % 3 == 0)
                        {
                            // depth = 6 -> low
                        }
                    }
                }
            }
        }
    }

    public void LongFunction()
    {
        var x0 = 0;
        var x1 = 0;
        var x2 = 0;
        var x3 = 0;
        var x4 = 0;
        var x5 = 0;
        var x6 = 0;
        var x7 = 0;
        var x8 = 0;
        var x9 = 0;
        var x10 = 0;
        var x11 = 0;
        var x12 = 0;
        var x13 = 0;
        var x14 = 0;
        var x15 = 0;
        var x16 = 0;
        var x17 = 0;
        var x18 = 0;
        var x19 = 0;
        var x20 = 0;
        var x21 = 0;
        var x22 = 0;
        var x23 = 0;
        var x24 = 0;
        var x25 = 0;
        var x26 = 0;
        var x27 = 0;
        var x28 = 0;
        var x29 = 0;
        var x30 = 0;
        var x31 = 0;
        var x32 = 0;
        var x33 = 0;
        var x34 = 0;
        var x35 = 0;
        var x36 = 0;
        var x37 = 0;
        var x38 = 0;
        var x39 = 0;
        var x40 = 0;
        var x41 = 0;
        var x42 = 0;
        var x43 = 0;
        var x44 = 0;
        var x45 = 0;
        var x46 = 0;
        var x47 = 0;
        var x48 = 0;
        var x49 = 0;
        var x50 = 0;
        var x51 = 0;
    }
}
