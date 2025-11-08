using System;
using System.Reflection;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
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
        for (int i = 0; i < 10; i++)
        {
            removeSizeof();
            removeEmptyType();
            removeChecked();
            //transformStoI4();
            removeAdd();
            removeMul();
            removeSub();
            removeXOR();
            decryptStrings();
        }
    }

    // method to get previous non-NOP instruction
    private CilInstruction GetPrevNonNop(CilInstructionCollection instructions, int startIndex, out int foundIndex)
    {
        foundIndex = -1;
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (instructions[i].OpCode != CilOpCodes.Nop)
            {
                foundIndex = i;
                return instructions[i];
            }
        }
        return null;
    }

    private bool getIntegerOperand(CilInstruction inst, out int value)
    {
        value = 0;

        // Handle ldc.i4 variants
        if (inst.OpCode == CilOpCodes.Ldc_I4)
            value = (int)inst.Operand;
        else if (inst.OpCode == CilOpCodes.Ldc_I4_S)
            value = (int)(sbyte)inst.Operand;
        else if (inst.OpCode == CilOpCodes.Ldc_I4_0)
            value = 0;
        else if (inst.OpCode == CilOpCodes.Ldc_I4_1)
            value = 1;
        else if (inst.OpCode == CilOpCodes.Ldc_I4_2)
            value = 2;
        else if (inst.OpCode == CilOpCodes.Ldc_I4_3)
            value = 3;
        else if (inst.OpCode == CilOpCodes.Ldc_I4_4)
            value = 4;
        else if (inst.OpCode == CilOpCodes.Ldc_I4_5)
            value = 5;
        else if (inst.OpCode == CilOpCodes.Ldc_I4_6)
            value = 6;
        else if (inst.OpCode == CilOpCodes.Ldc_I4_7)
            value = 7;
        else if (inst.OpCode == CilOpCodes.Ldc_I4_8)
            value = 8;
        else if (inst.OpCode == CilOpCodes.Ldc_I4_M1)
            value = -1;
        // Handle ldc.i8 (int64) - may truncate
        else if (inst.OpCode == CilOpCodes.Ldc_I8)
        {
            long longValue = (long)inst.Operand;
            value = (int)longValue; // Truncates if value is outside int32 range
        }
        // Handle ldc.r4 (float) - converts to int
        else if (inst.OpCode == CilOpCodes.Ldc_R4)
        {
            float floatValue = (float)inst.Operand;
            value = (int)floatValue; // Truncates decimal part
        }
        // Handle ldc.r8 (double) - converts to int
        else if (inst.OpCode == CilOpCodes.Ldc_R8)
        {
            double doubleValue = (double)inst.Operand;
            value = (int)doubleValue; // Truncates decimal part
        }
        else
            return false;

        return true;
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
                                    if (current_inst.OpCode == CilOpCodes.Nop)
                                    {
                                        pointer--;
                                        continue;
                                    }
                                    if (current_inst.OpCode.Mnemonic.StartsWith("conv."))
                                    {
                                        pointer--;
                                        current_inst.ReplaceWithNop();
                                        continue;
                                    }

                                    if (!current_inst.OpCode.Mnemonic.StartsWith("ldc."))
                                    {
                                        break;
                                    }
                                    arguments.Insert(0, Convert.ToInt32(current_inst.Operand));
                                    indices.Insert(0, pointer);
                                    argsFound++;
                                    pointer--;
                                }

                                
                                while (pointer >= 0 && body.Instructions[pointer].OpCode == CilOpCodes.Nop)
                                {
                                    pointer--;
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

                                    int idx1, idx2;
                                    var prev_inst = GetPrevNonNop(body.Instructions, index, out idx1);
                                    var prev_inst2 = GetPrevNonNop(body.Instructions, idx1, out idx2);

                                    if (prev_inst == null || prev_inst2 == null)
                                        continue;

                                    int val1, val2;
                                    if (!getIntegerOperand(prev_inst, out val1) || !getIntegerOperand(prev_inst2, out val2))
                                        continue;
                                    /*
                                    if (prev_inst.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val1 = (int)(sbyte)(prev_inst.Operand);
                                    }
                                    else if (prev_inst.OpCode == CilOpCodes.Ldc_I4)
                                    {
                                        val1 = (int)(prev_inst.Operand);
                                    }
                                    else if (!getIntegerOperand(prev_inst, out val1))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        continue;
                                    }

                                    if (prev_inst2.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val2 = (int)(sbyte)(prev_inst2.Operand);
                                    }
                                    else if (prev_inst2.OpCode == CilOpCodes.Ldc_I4)
                                    {
                                        val2 = (int)(prev_inst2.Operand);
                                    }
                                    else if (!getIntegerOperand(prev_inst2, out val2))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        continue;
                                    } */


                                    int xor = val1 ^ val2;

                                    body.Instructions[idx1].ReplaceWithNop();
                                    body.Instructions[idx2].ReplaceWithNop();
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
                            // current_inst.OpCode.Mnemonic.StartsWith("ldc.")
                            if (instruction.OpCode == CilOpCodes.Add ||
                                instruction.OpCode == CilOpCodes.Add_Ovf ||
                                instruction.OpCode == CilOpCodes.Add_Ovf_Un)
                            {
                                try
                                {
                                    var index = body.Instructions.IndexOf(instruction);

                                    int idx1, idx2;
                                    var prev_inst = GetPrevNonNop(body.Instructions, index, out idx1);
                                    var prev_inst2 = GetPrevNonNop(body.Instructions, idx1, out idx2);

                                    if (prev_inst == null || prev_inst2 == null)
                                        continue;

                                    int val1, val2;
                                    if (!getIntegerOperand(prev_inst, out val1) || !getIntegerOperand(prev_inst2, out val2))
                                        continue;
                                    /*
                                    if (prev_inst.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val1 = (int)(sbyte)(prev_inst.Operand);
                                    }
                                    else if (prev_inst.OpCode == CilOpCodes.Ldc_I4)
                                    {
                                        val1 = (int)(prev_inst.Operand);
                                    }else if(prev_inst.OpCode == CilOpCodes.Ldc_I8)
                                    {
                                        val1 = (int)(prev_inst.Operand);
                                    }
                                    else if(!getIntegerOperand(prev_inst, out val1))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        continue;
                                    }

                                    if (prev_inst2.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val2 = (int)(sbyte)(prev_inst2.Operand);
                                    }
                                    else if (prev_inst2.OpCode == CilOpCodes.Ldc_I4)
                                    {
                                        val2 = (int)(prev_inst2.Operand);
                                    }
                                    else if (prev_inst2.OpCode == CilOpCodes.Ldc_I8)
                                    {
                                        val2 = (int)(prev_inst2.Operand);
                                    }
                                    else if (!getIntegerOperand(prev_inst2, out val2))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                    */

                                    int sum = val1 + val2;

                                    body.Instructions[idx1].ReplaceWithNop();
                                    body.Instructions[idx2].ReplaceWithNop();
                                    instruction.ReplaceWith(CilOpCodes.Ldc_I4, (int)sum);
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

    private void removeMul()
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
                            // current_inst.OpCode.Mnemonic.StartsWith("ldc.")
                            if (instruction.OpCode == CilOpCodes.Mul ||
                                instruction.OpCode == CilOpCodes.Mul_Ovf ||
                                instruction.OpCode == CilOpCodes.Mul_Ovf_Un)
                            {
                                try
                                {
                                    var index = body.Instructions.IndexOf(instruction);

                                    int idx1, idx2;
                                    var prev_inst = GetPrevNonNop(body.Instructions, index, out idx1);
                                    var prev_inst2 = GetPrevNonNop(body.Instructions, idx1, out idx2);

                                    if (prev_inst == null || prev_inst2 == null)
                                        continue;

                                    int val1, val2;
                                    if (!getIntegerOperand(prev_inst, out val1) || !getIntegerOperand(prev_inst2, out val2))
                                        continue;
                                    /*
                                    if (prev_inst.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val1 = (int)(sbyte)(prev_inst.Operand);
                                    }
                                    else if (prev_inst.OpCode == CilOpCodes.Ldc_I4)
                                    {
                                        val1 = (int)(prev_inst.Operand);
                                    }
                                    else if (prev_inst.OpCode == CilOpCodes.Ldc_I8)
                                    {
                                        val1 = (int)(prev_inst.Operand);
                                    }
                                    else if (!getIntegerOperand(prev_inst, out val1))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        continue;
                                    }

                                    if (prev_inst2.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val2 = (int)(sbyte)(prev_inst2.Operand);
                                    }
                                    else if (prev_inst2.OpCode == CilOpCodes.Ldc_I4)
                                    {
                                        val2 = (int)(prev_inst2.Operand);
                                    }
                                    else if (prev_inst2.OpCode == CilOpCodes.Ldc_I8)
                                    {
                                        val2 = (int)(prev_inst2.Operand);
                                    }
                                    else if (!getIntegerOperand(prev_inst2, out val2))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                    */

                                    int mul = val1 * val2;

                                    body.Instructions[idx1].ReplaceWithNop();
                                    body.Instructions[idx2].ReplaceWithNop();
                                    instruction.ReplaceWith(CilOpCodes.Ldc_I4, (int)mul);
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

    private void removeSub()
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
                            // current_inst.OpCode.Mnemonic.StartsWith("ldc.")
                            if (instruction.OpCode == CilOpCodes.Sub ||
                                instruction.OpCode == CilOpCodes.Sub_Ovf ||
                                instruction.OpCode == CilOpCodes.Sub_Ovf_Un)
                            {
                                try
                                {
                                    var index = body.Instructions.IndexOf(instruction);

                                    int idx1, idx2;
                                    var prev_inst = GetPrevNonNop(body.Instructions, index, out idx1);
                                    var prev_inst2 = GetPrevNonNop(body.Instructions, idx1, out idx2);

                                    if (prev_inst == null || prev_inst2 == null)
                                        continue;

                                    int val1, val2;
                                    if (!getIntegerOperand(prev_inst, out val1) || !getIntegerOperand(prev_inst2, out val2))
                                        continue;
                                    /*
                                    if (prev_inst.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val1 = (int)(sbyte)(prev_inst.Operand);
                                    }
                                    else if (prev_inst.OpCode == CilOpCodes.Ldc_I4)
                                    {
                                        val1 = (int)(prev_inst.Operand);
                                    }
                                    else if (prev_inst.OpCode == CilOpCodes.Ldc_I8)
                                    {
                                        val1 = (int)(prev_inst.Operand);
                                    }
                                    else if (!getIntegerOperand(prev_inst, out val1))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        continue;
                                    }

                                    if (prev_inst2.OpCode == CilOpCodes.Ldc_I4_S)
                                    {
                                        val2 = (int)(sbyte)(prev_inst2.Operand);
                                    }
                                    else if (prev_inst2.OpCode == CilOpCodes.Ldc_I4)
                                    {
                                        val2 = (int)(prev_inst2.Operand);
                                    }
                                    else if (prev_inst2.OpCode == CilOpCodes.Ldc_I8)
                                    {
                                        val2 = (int)(prev_inst2.Operand);
                                    }
                                    else if (!getIntegerOperand(prev_inst2, out val2))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                    */

                                    int diff = val2 - val1;
                                    body.Instructions[idx1].ReplaceWithNop();
                                    body.Instructions[idx2].ReplaceWithNop();
                                    instruction.ReplaceWith(CilOpCodes.Ldc_I4, (int)diff);
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
                                // not needed to check previous instruction for this operation
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

                                int prevIdx;
                                var previous_inst = GetPrevNonNop(body.Instructions, index, out prevIdx);

                                if (previous_inst == null)
                                    continue;

                                if (previous_inst.Operand is AsmResolver.DotNet.IFieldDescriptor field
                                        && field.Name == "EmptyTypes"
                                        && field.DeclaringType?.FullName == "System.Type")
                                {
                                    //Console.WriteLine(previous_inst);
                                    instruction.ReplaceWith(CilOpCodes.Ldc_I4, 0);
                                    body.Instructions[prevIdx].ReplaceWithNop();
                                    body.ComputeMaxStack(true);
                                }
                            }
                        }
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
        var method_ = assembly.ManifestModule.ResolveMethod(token) as MethodInfo;

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
