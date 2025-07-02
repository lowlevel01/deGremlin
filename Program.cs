
using System;
using System.Reflection;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;



public class Program
{
    public static void Main(string[] args)
    {

        const string banner = @"
       __          ______                              __    _            
      |  ]       .' ___  |                            [  |  (_)           
  .--.| | .---. / .'   \_| _ .--.  .---.  _ .--..--.   | |  __   _ .--.   
/ /'`\' |/ /__\\| |   ____[ `/'`\]/ /__\\[ `.-. .-. |  | | [  | [ `.-. |  
| \__/  || \__.,\ `.___]  || |    | \__., | | | | | |  | |  | |  | | | |  
 '.__.;__]'.__.' `._____.'[___]    '.__.'[___||__||__][___][___][___||__] 
                                                                        P.S: little bit Appfuscator                   
";
        Console.WriteLine(banner);


        if (args.Length < 2) {

            Console.WriteLine("Usage:");
            Console.WriteLine("degremlin.exe [filepath] [method_token]");
            System.Environment.Exit(0);

        }

        string filepath = args[0];
        int token = Convert.ToInt32(args[1], 16);
        Assembly assembly1 = Assembly.LoadFrom(filepath);
        var module = ModuleDefinition.FromFile(filepath);

        var degremlin = new DeGremlin(assembly1, module, token);
        degremlin.Process();




        var assembly = Assembly.LoadFrom(filepath);
                                                             

        string result = MethodInvoker.InvokeMethod(assembly, 0x06000003, new object[] { 6902, 19776, 231 });
        //Console.WriteLine(result);
        string filename = Path.GetFileName(filepath);
        filepath = filepath.Replace(filename, $"patched_{filename}");
        module.Write(filepath);
        Console.WriteLine($"Strings patched successfully! Written to {filepath}");

    }
}

