using System;
using XanaduYonder.EditorTools;

internal static class Program
{
    private static int Main()
    {
        try
        {
            PersistenceChecks.Run();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
