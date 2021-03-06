﻿// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Driver.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        private delegate void InstructionHandler(CodeGenerator generator, ILInstruction instruction);
        private static readonly Dictionary<ILInstructionType, InstructionHandler> InstructionHandlers = new Dictionary<ILInstructionType, InstructionHandler>();

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        [SuppressMessage("Microsoft.Maintainability", "CA1505:AvoidUnmaintainableCode")]
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        static CodeGenerator()
        {
            InstructionHandlers.Add(ILInstructionType.Nop, (generator, instruction) => { });
            InstructionHandlers.Add(ILInstructionType.Break, (generator, instruction) => generator.MakeTrap());

            InstructionHandlers.Add(ILInstructionType.Ldarg, (generator, instruction) => generator.LoadVariable(new VariableRef(instruction.GetArgumentAs<int>(), VariableRefType.Argument)));
            InstructionHandlers.Add(ILInstructionType.Ldarga, (generator, instruction) => generator.LoadVariableAddress(new VariableRef(instruction.GetArgumentAs<int>(), VariableRefType.Argument)));
            InstructionHandlers.Add(ILInstructionType.Starg, (generator, instruction) => generator.StoreVariable(new VariableRef(instruction.GetArgumentAs<int>(), VariableRefType.Argument)));

            InstructionHandlers.Add(ILInstructionType.Ldloc, (generator, instruction) => generator.LoadVariable(new VariableRef(instruction.GetArgumentAs<int>(), VariableRefType.Local)));
            InstructionHandlers.Add(ILInstructionType.Ldloca, (generator, instruction) => generator.LoadVariableAddress(new VariableRef(instruction.GetArgumentAs<int>(), VariableRefType.Local)));
            InstructionHandlers.Add(ILInstructionType.Stloc, (generator, instruction) => generator.StoreVariable(new VariableRef(instruction.GetArgumentAs<int>(), VariableRefType.Local)));

            InstructionHandlers.Add(ILInstructionType.Ldnull, (generator, instruction) => generator.LoadNull());
            InstructionHandlers.Add(ILInstructionType.LdI4, (generator, instruction) => generator.Load(instruction.GetArgumentAs<int>()));
            InstructionHandlers.Add(ILInstructionType.LdI8, (generator, instruction) => generator.Load(instruction.GetArgumentAs<long>()));
            InstructionHandlers.Add(ILInstructionType.LdR4, (generator, instruction) => generator.Load(instruction.GetArgumentAs<float>()));
            InstructionHandlers.Add(ILInstructionType.LdR8, (generator, instruction) => generator.Load(instruction.GetArgumentAs<double>()));
            InstructionHandlers.Add(ILInstructionType.Ldstr, (generator, instruction) => generator.LoadString(instruction.GetArgumentAs<string>()));

            InstructionHandlers.Add(ILInstructionType.Dup, (generator, instruction) => generator.CurrentBlock.Dup());
            InstructionHandlers.Add(ILInstructionType.Pop, (generator, instruction) => generator.CurrentBlock.Pop());
            InstructionHandlers.Add(ILInstructionType.Ret, (generator, instruction) => generator.MakeReturn());
            InstructionHandlers.Add(ILInstructionType.Call, (generator, instruction) => generator.MakeCall(instruction.GetArgumentAs<MethodBase>()));
            InstructionHandlers.Add(ILInstructionType.Callvirt, (generator, instruction) =>
            {
                var method = instruction.GetArgumentAs<MethodInfo>();
                if (instruction.HasFlags(ILInstructionFlags.Constrained))
                    generator.MakeVirtualCall(method, instruction.FlagsContext.Argument as Type);
                else
                    generator.MakeVirtualCall(method, null);
            });
            InstructionHandlers.Add(ILInstructionType.Calli, (generator, instruction) => generator.MakeCalli(instruction.Argument));
            InstructionHandlers.Add(ILInstructionType.Jmp, (generator, instruction) => generator.MakeJump(instruction.GetArgumentAs<MethodBase>()));

            InstructionHandlers.Add(ILInstructionType.Br, (generator, instruction) => generator.MakeBranch(instruction.GetArgumentAs<ILInstructionBranchTargets>()));

            InstructionHandlers.Add(ILInstructionType.Brfalse, (generator, instruction) =>
            {
                generator.CurrentBlock.Push(typeof(bool), ConstInt(generator.LLVMContext.Int1Type, 0, false));
                generator.MakeBranch(instruction.GetArgumentAs<ILInstructionBranchTargets>(), CompareType.Equal, true);
            });
            InstructionHandlers.Add(ILInstructionType.Brtrue, (generator, instruction) =>
            {
                generator.CurrentBlock.Push(typeof(bool), ConstInt(generator.LLVMContext.Int1Type, 1, false));
                generator.MakeBranch(instruction.GetArgumentAs<ILInstructionBranchTargets>(), CompareType.Equal, true);
            });

            InstructionHandlers.Add(ILInstructionType.Beq, (generator, instruction) => generator.MakeBranch(instruction.GetArgumentAs<ILInstructionBranchTargets>(), CompareType.Equal, instruction.HasFlags(ILInstructionFlags.Unsigned)));
            InstructionHandlers.Add(ILInstructionType.Bne, (generator, instruction) => generator.MakeBranch(instruction.GetArgumentAs<ILInstructionBranchTargets>(), CompareType.NotEqual, instruction.HasFlags(ILInstructionFlags.Unsigned)));
            InstructionHandlers.Add(ILInstructionType.Bge, (generator, instruction) => generator.MakeBranch(instruction.GetArgumentAs<ILInstructionBranchTargets>(), CompareType.GreaterEqual, instruction.HasFlags(ILInstructionFlags.Unsigned)));
            InstructionHandlers.Add(ILInstructionType.Bgt, (generator, instruction) => generator.MakeBranch(instruction.GetArgumentAs<ILInstructionBranchTargets>(), CompareType.GreaterThan, instruction.HasFlags(ILInstructionFlags.Unsigned)));
            InstructionHandlers.Add(ILInstructionType.Ble, (generator, instruction) => generator.MakeBranch(instruction.GetArgumentAs<ILInstructionBranchTargets>(), CompareType.LessEqual, instruction.HasFlags(ILInstructionFlags.Unsigned)));
            InstructionHandlers.Add(ILInstructionType.Blt, (generator, instruction) => generator.MakeBranch(instruction.GetArgumentAs<ILInstructionBranchTargets>(), CompareType.LessThan, instruction.HasFlags(ILInstructionFlags.Unsigned)));

            InstructionHandlers.Add(ILInstructionType.Switch, (generator, instruction) => generator.MakeSwitch(instruction.GetArgumentAs<ILInstructionBranchTargets>()));

            InstructionHandlers.Add(ILInstructionType.Add, (generator, instruction) => generator.MakeArithmeticAdd(instruction.HasFlags(ILInstructionFlags.Overflow)));
            InstructionHandlers.Add(ILInstructionType.Sub, (generator, instruction) => generator.MakeArithmeticSub(instruction.HasFlags(ILInstructionFlags.Overflow)));
            InstructionHandlers.Add(ILInstructionType.Mul, (generator, instruction) => generator.MakeArithmeticMul(instruction.HasFlags(ILInstructionFlags.Overflow)));
            InstructionHandlers.Add(ILInstructionType.Div, (generator, instruction) => generator.MakeArithmeticDiv(instruction.HasFlags(ILInstructionFlags.Unsigned)));
            InstructionHandlers.Add(ILInstructionType.Rem, (generator, instruction) => generator.MakeArithmeticRem(instruction.HasFlags(ILInstructionFlags.Unsigned)));
            InstructionHandlers.Add(ILInstructionType.And, (generator, instruction) => generator.MakeNumericAnd());
            InstructionHandlers.Add(ILInstructionType.Or, (generator, instruction) => generator.MakeNumericOr());
            InstructionHandlers.Add(ILInstructionType.Xor, (generator, instruction) => generator.MakeNumericXor());
            InstructionHandlers.Add(ILInstructionType.Shl, (generator, instruction) => generator.MakeNumericShl());
            InstructionHandlers.Add(ILInstructionType.Shr, (generator, instruction) => generator.MakeNumericShr(instruction.HasFlags(ILInstructionFlags.Unsigned)));
            InstructionHandlers.Add(ILInstructionType.Neg, (generator, instruction) => generator.MakeNumericNeg());
            InstructionHandlers.Add(ILInstructionType.Not, (generator, instruction) => generator.MakeNumericNot());

            InstructionHandlers.Add(ILInstructionType.Conv, (generator, instruction) =>
            {
                var targetType = instruction.GetArgumentAs<Type>();
                generator.MakeConvert(targetType, instruction.HasFlags(ILInstructionFlags.Unsigned));
            });

            InstructionHandlers.Add(ILInstructionType.Initobj, (generator, instruction) => generator.MakeInitObject(instruction.GetArgumentAs<Type>()));
            InstructionHandlers.Add(ILInstructionType.Newobj, (generator, instruction) => generator.MakeNewObject(instruction.GetArgumentAs<MethodBase>()));
            InstructionHandlers.Add(ILInstructionType.Newarr, (generator, instruction) => generator.MakeNewArray(instruction.GetArgumentAs<Type>()));
            InstructionHandlers.Add(ILInstructionType.Castclass, (generator, instruction) => generator.MakeCastClass(instruction.GetArgumentAs<Type>()));
            InstructionHandlers.Add(ILInstructionType.Isinst, (generator, instruction) => generator.MakeIsInstance(instruction.GetArgumentAs<Type>()));
            InstructionHandlers.Add(ILInstructionType.Ldlen, (generator, instruction) => generator.LoadArrayLength());
            InstructionHandlers.Add(ILInstructionType.Box, (generator, instruction) => generator.MakeBox());
            InstructionHandlers.Add(ILInstructionType.Unbox, (generator, instruction) => generator.MakeUnbox(instruction.GetArgumentAs<Type>()));

            InstructionHandlers.Add(ILInstructionType.Ldfld, (generator, instruction) => generator.LoadField(instruction.GetArgumentAs<FieldInfo>()));
            InstructionHandlers.Add(ILInstructionType.Ldsfld, (generator, instruction) => generator.LoadStaticField(instruction.GetArgumentAs<FieldInfo>()));
            InstructionHandlers.Add(ILInstructionType.Ldflda, (generator, instruction) => generator.LoadFieldAddress(instruction.GetArgumentAs<FieldInfo>()));
            InstructionHandlers.Add(ILInstructionType.Ldsflda, (generator, instruction) => generator.LoadStaticFieldAddress(instruction.GetArgumentAs<FieldInfo>()));
            InstructionHandlers.Add(ILInstructionType.Stfld, (generator, instruction) => generator.StoreField(instruction.GetArgumentAs<FieldInfo>()));
            InstructionHandlers.Add(ILInstructionType.Stsfld, (generator, instruction) => generator.StoreStaticField(instruction.GetArgumentAs<FieldInfo>()));

            InstructionHandlers.Add(ILInstructionType.Ceq, (generator, instruction) => generator.MakeCompare(CompareType.Equal, instruction.HasFlags(ILInstructionFlags.Unsigned)));
            InstructionHandlers.Add(ILInstructionType.Cgt, (generator, instruction) => generator.MakeCompare(CompareType.GreaterThan, instruction.HasFlags(ILInstructionFlags.Unsigned)));
            InstructionHandlers.Add(ILInstructionType.Clt, (generator, instruction) => generator.MakeCompare(CompareType.LessThan, instruction.HasFlags(ILInstructionFlags.Unsigned)));

            InstructionHandlers.Add(ILInstructionType.Ldelem, (generator, instruction) => generator.LoadArrayElement(instruction.GetArgumentAs<Type>()));
            InstructionHandlers.Add(ILInstructionType.Ldelema, (generator, instruction) => generator.LoadArrayElementAddress());
            InstructionHandlers.Add(ILInstructionType.Stelem, (generator, instruction) => generator.StoreArrayElement(instruction.GetArgumentAs<Type>()));

            InstructionHandlers.Add(ILInstructionType.Stind, (generator, instruction) => generator.StoreIndirect(instruction.GetArgumentAs<Type>()));
            InstructionHandlers.Add(ILInstructionType.Ldind, (generator, instruction) => generator.LoadIndirect(instruction.GetArgumentAs<Type>()));

            InstructionHandlers.Add(ILInstructionType.Ldobj, (generator, instruction) => generator.MakeLoadObject(instruction.GetArgumentAs<Type>()));
            InstructionHandlers.Add(ILInstructionType.Stobj, (generator, instruction) => generator.MakeStoreObject(instruction.GetArgumentAs<Type>()));

            InstructionHandlers.Add(ILInstructionType.Localloc, (generator, instruction) => generator.MakeLocalAlloc());
        }
    }
}
