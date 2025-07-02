using System;
using System.Reflection;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;



public class DeGremlin
{
    private readonly ModuleDefinition module;
    private readonly int token;
    private readonly Assembly assembly;
    private static readonly CilOpCode[] ConvOvfOpCodes = new[]
    {
        CilOpCodes.Conv_Ovf_I1,
        CilOpCodes.Conv_Ovf_I2,
        CilOpCodes.Conv_Ovf_I4,
        CilOpCodes.Conv_Ovf_I8,
        CilOpCodes.Conv_Ovf_U1,
        CilOpCodes.Conv_Ovf_U2,
        CilOpCodes.Conv_Ovf_U4,
        CilOpCodes.Conv_Ovf_U8,
        CilOpCodes.Conv_Ovf_I,
        CilOpCodes.Conv_Ovf_U
    };


    public DeGremlin(Assembly _assembly, ModuleDefinition _module, int _token)
    {
        module = _module;
        token = _token;
        assembly = _assembly;
    }

    public void Process()
    {
        removeSizeof();
        removeEmptyType();
        removeChecked();
        //transformStoI4();
        removeAdd();
        removeXOR();
        decryptStrings();
    }

    private void decryptStrings()
    {
        foreach (var type in module.GetAllTypes())
        {
            foreach (var method in type.Methods)
            {
                if (method.HasMethodBody && method.CilMethodBody is { } body)
                {
                    var instructions = body.Instructions;
                    for (int i = instructions.Count - 1; i > 0; i--)
                    {
                        var instruction = instructions[i];
                        if (instruction.OpCode == CilOpCodes.Call)
                        {
                            if (instruction.Operand is IMethodDescriptor calledMethod && token == calledMethod.MetadataToken)
                            {
                                var index = body.Instructions.IndexOf(instruction);
                                List<int> arguments = new List<int>();
                                List<int> indices = new List<int>();

                                int argsFound = 0;
                                int pointer = index - 1;
                                while (pointer >= 0 && argsFound < 3)
                                {
                                    var current_inst = body.Instructions[pointer];
                                    if (current_inst.OpCode != CilOpCodes.Nop && current_inst.OpCode != CilOpCodes.Ldc_I4)
                                    {
                                        break;
                                    }
                                    if (current_inst.OpCode == CilOpCodes.Nop)
                                    {
                                        pointer--;
                                        continue;
                                    }
                                    arguments.Insert(0, Convert.ToInt32(current_inst.Operand));
                                    indices.Insert(0, pointer);
                                    argsFound++;
                                    pointer--;
                                }

                                if (pointer > 0 && body.Instructions[pointer - 1].OpCode != CilOpCodes.Nop)
                                {
                                    continue;
                                }

                                if (arguments.Count == 3)
                                {
                                    var originalInstructions = indices.ToDictionary(
                                        idx => idx,
                                        idx => new {
                                            OpCode = body.Instructions[idx].OpCode,
                                            Operand = body.Instructions[idx].Operand
                                        });
                                    var originalCallOpCode = instruction.OpCode;
                                    var originalCallOperand = instruction.Operand;

                                    try
                                    {
                                        object[] argsArray = arguments.Select(x => (object)x).ToArray();
                                        string decryptedString = MethodInvoker.InvokeMethod(assembly, token, argsArray);
                                        //Console.WriteLine(decryptedString);

                                        try
                                        {

                                            body.Instructions[indices[0]].ReplaceWithNop();
                                            body.Instructions[indices[1]].ReplaceWithNop();
                                            body.Instructions[indices[2]].ReplaceWithNop();
                                            instruction.OpCode = CilOpCodes.Ldstr;
                                            instruction.Operand = decryptedString;
                                            body.ComputeMaxStack();
                                            //Console.WriteLine($"->{body.Instructions[pointer].OpCode}");
                                        }
                                        catch (Exception ex)
                                        {
                                            //Console.WriteLine("Exception during instruction modification");

                                            foreach (var idx in indices)
                                            {
                                                if (originalInstructions.TryGetValue(idx, out var original))
                                                {
                                                    body.Instructions[idx].OpCode = original.OpCode;
                                                    body.Instructions[idx].Operand = original.Operand;
                                                }
                                            }
                                            instruction.OpCode = originalCallOpCode;
                                            instruction.Operand = originalCallOperand;

                                            continue;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        //Console.WriteLine("Exception during decryption");
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    private void removeXOR()
    {
        foreach (var type in module.GetAllTypes())
        {
            foreach (var method in type.Methods)
            {
                //Console.WriteLine($"{method.Name} : {method.MetadataToken}");

                if (method.HasMethodBody)
                {
                    if (method.CilMethodBody is { } body)
                    {
                        foreach (var instruction in body.Instructions)
                        {
                            if (instruction.OpCode == CilOpCodes.Xor)
                            {
                                try
                                {
                                    var index = body.Instructions.IndexOf(instruction);
                                    var prev_inst = body.Instructions[index - 1];
                                    var prev_inst2 = body.Instructions[index - 2];
                                    int val1, val2;
                                    if (prev_inst.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val1 = (int)(sbyte)(prev_inst.Operand);
                                    }
                                    else
                                    {
                                        val1 = (int)(prev_inst.Operand);
                                    }
                                    if (prev_inst2.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val2 = (int)(sbyte)(prev_inst2.Operand);
                                    }
                                    else
                                    {
                                        val2 = (int)(prev_inst2.Operand);
                                    }


                                    int xor = val1 ^ val2;

                                    prev_inst.ReplaceWithNop();
                                    prev_inst2.ReplaceWithNop();
                                    instruction.ReplaceWith(CilOpCodes.Ldc_I4, (int)xor);
                                    //Console.WriteLine(prev_inst);
                                }
                                catch (Exception ex) { continue; }
                            }
                            //Console.WriteLine($"0x{Convert.ToString(instruction.Offset, 16)}");

                        }
                    }
                }
            }
        }
    }
    private void removeAdd()
    {
        foreach (var type in module.GetAllTypes())
        {
            foreach (var method in type.Methods)
            {
                //Console.WriteLine($"{method.Name} : {method.MetadataToken}");

                if (method.HasMethodBody)
                {
                    if (method.CilMethodBody is { } body)
                    {
                        foreach (var instruction in body.Instructions)
                        {
                            if (instruction.OpCode == CilOpCodes.Add)
                            {
                                try
                                {
                                    var index = body.Instructions.IndexOf(instruction);
                                    var prev_inst = body.Instructions[index - 1];
                                    var prev_inst2 = body.Instructions[index - 2];
                                    int val1, val2;
                                    if(prev_inst.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val1 = (int)(sbyte)(prev_inst.Operand);
                                    }
                                    else
                                    {
                                        val1 = (int)(prev_inst.Operand);
                                    }
                                    if (prev_inst2.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val2 = (int)(sbyte)(prev_inst2.Operand);
                                    }
                                    else
                                    {
                                        val2 = (int)(prev_inst2.Operand);
                                    }


                                    int sum = val1 + val2;
                                    
                                    prev_inst.ReplaceWithNop();
                                    prev_inst2.ReplaceWithNop();
                                    instruction.ReplaceWith(CilOpCodes.Ldc_I4, (int)sum);
                                    //Console.WriteLine(prev_inst);
                                }catch(Exception ex) {continue;}
                            }
                            //Console.WriteLine($"0x{Convert.ToString(instruction.Offset, 16)}");

                        }
                    }
                }
            }
        }
    }
    private void removeChecked()
    {
        foreach (var type in module.GetAllTypes())
        {
            foreach (var method in type.Methods)
            {
                //Console.WriteLine($"{method.Name} : {method.MetadataToken}");

                if (method.HasMethodBody)
                {
                    if (method.CilMethodBody is { } body)
                    {
                        foreach (var instruction in body.Instructions)
                        {
                            if (Array.Exists(ConvOvfOpCodes, op => op == instruction.OpCode))
                            {
                                instruction.ReplaceWithNop();
                                var index = body.Instructions.IndexOf(instruction);
                                var prev_inst = body.Instructions[index-1];
                                //Console.WriteLine(prev_inst);
                            }
                            //Console.WriteLine($"0x{Convert.ToString(instruction.Offset, 16)}");

                        }
                    }
                }
            }
        }
    }
    private void removeEmptyType()
    {
        foreach (var type in module.GetAllTypes())
        {
            foreach (var method in type.Methods)
            {
                //Console.WriteLine($"{method.Name} : {method.MetadataToken}");

                if (method.HasMethodBody)
                {
                    if (method.CilMethodBody is { } body)
                    {
                        foreach (var instruction in body.Instructions)
                        {
                         


                                // Deobfuscating System.Type::EmptyTypes
                                if (instruction.OpCode == CilOpCodes.Ldlen)
                                {
                                    var index = body.Instructions.IndexOf(instruction);
                                    var previous_inst = body.Instructions[index - 1];




                                    if (previous_inst.Operand is AsmResolver.DotNet.IFieldDescriptor field
                                            && field.Name == "EmptyTypes"
                                            && field.DeclaringType?.FullName == "System.Type")
                                    {
                                        //Console.WriteLine(previous_inst);
                                        instruction.ReplaceWith(CilOpCodes.Ldc_I4, 0);
                                        previous_inst.ReplaceWithNop();
                                        body.ComputeMaxStack(true);
                                    }

                                }
                            }
                            //Console.WriteLine($"0x{Convert.ToString(instruction.Offset, 16)}");

                        
                    }
                }
            }
        }
    }
    private void removeSizeof()
    {
        foreach (var type in module.GetAllTypes())
        {
            foreach (var method in type.Methods)
            {
                //Console.WriteLine($"{method.Name} : {method.MetadataToken}");

                if (method.HasMethodBody)
                {
                    if (method.CilMethodBody is { } body)
                    {
                        foreach (var instruction in body.Instructions)
                        {
                            if (instruction.OpCode == CilOpCodes.Sizeof)
                            {
                                // Deobfuscating sizeof()
                                if (instruction.Operand is AsmResolver.DotNet.TypeReference typeRef && typeRef.Namespace == "System")
                                {
                                    instruction.OpCode = CilOpCodes.Ldc_I4;
                                    int size = -1;
                                    switch (typeRef.Name)
                                    {
                                        case "Guid":
                                            size = 16;
                                            break;
                                        case "Int64":
                                            size = 8;
                                            break;
                                        case "Int32":
                                            size = 4;
                                            break;
                                        case "UInt64":
                                            size = 8;
                                            break;
                                        case "UInt32":
                                            size = 4;
                                            break;
                                        case "Byte":
                                            size = 1;
                                            break;
                                        case "Double":
                                            size = 8;
                                            break;
                                        case "Single":
                                            size = 4;
                                            break;
                                        case "Int16":
                                            size = 2;
                                            break;
                                        case "UInt16":
                                            size = 2;
                                            break;
                                        default:
                                            //Console.WriteLine(instruction.Operand);
                                            break;
                                    }

                                    if (size > 0)
                                    {
                                        instruction.ReplaceWith(CilOpCodes.Ldc_I4, (int)size);
                                    }

                                }



                            }
                        }


                    }
                }
            }
        }
    }
}
public static class MethodInvoker
{
    public static string InvokeMethod(Assembly assembly, int token, params object[] args)
    {
        var method_ = assembly.ManifestModule.ResolveMethod(0x06000003) as MethodInfo;

        if (method_ == null)
            throw new InvalidOperationException("Method not found");

        // Verify the signature matches what we expect
        if (!method_.IsStatic ||
            method_.ReturnType != typeof(string) ||
            method_.GetParameters().Length != 3 ||
            method_.GetParameters().All(p => p.ParameterType != typeof(int)))
        {
            throw new InvalidOperationException("Method signature mismatch");
        }

        // Invoke with the specified parameters
        string result = (string)method_.Invoke(null, args);

        return result;
    }
}
