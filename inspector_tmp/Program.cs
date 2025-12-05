using System;
using System.Reflection;
class Program
{
    static void Main()
    {
        var type=Assembly.Load("CefNet").GetType("CefNet.CefNetApplication");
        foreach(var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            Console.WriteLine(method.Name);
        }
    }
}
