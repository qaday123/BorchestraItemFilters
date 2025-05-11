using Mono.Cecil.Cil;
using Mono.Cecil;
using MonoMod.Cil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BorchestraItemFilters
{
    /// <summary>
    /// IL-related extension methods.
    /// </summary>
    public static class ILTools
    {
        /// <summary>
        /// Moves the cursor after the next instruction that matches the given condition a given number of times.
        /// </summary>
        /// <param name="crs">The cursor to move.</param>
        /// <param name="match">Condition for the target instruction.</param>
        /// <param name="times">How many times the cursor should move.</param>
        /// <returns>True if all the moves were successful, false otherwise.</returns>
        public static bool JumpToNext(this ILCursor crs, Func<Instruction, bool> match, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                if (!crs.TryGotoNext(MoveType.After, match))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Moves the cursor before the next instruction that matches the given condition a given number of times.
        /// </summary>
        /// <param name="crs">The cursor to move.</param>
        /// <param name="match">Condition for the target instruction.</param>
        /// <param name="times">How many times the cursor should move.</param>
        /// <returns>True if all the moves were successful, false otherwise.</returns>
        public static bool JumpBeforeNext(this ILCursor crs, Func<Instruction, bool> match, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                if (!crs.TryGotoNext((i == (times - 1)) ? MoveType.Before : MoveType.After, match))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Moves after all instructions that match the given condition.
        /// </summary>
        /// <param name="crs">The cursor to move.</param>
        /// <param name="match">Condition for the target instruction.</param>
        /// <param name="matchFromStart">If true, this will check all instructions. If false, this will only check instructions after the cursor's current position.</param>
        /// <returns></returns>
        public static IEnumerable MatchAfter(this ILCursor crs, Func<Instruction, bool> match, bool matchFromStart = true)
        {
            var curr = crs.Next;

            if (matchFromStart)
                crs.Index = 0;

            while (crs.JumpToNext(match))
                yield return crs.Previous;

            crs.Next = curr;
        }

        /// <summary>
        /// Moves before all instructions that match the given condition.
        /// </summary>
        /// <param name="crs">The cursor to move.</param>
        /// <param name="match">Condition for the target instruction.</param>
        /// <param name="matchFromStart">If true, this will check all instructions. If false, this will only check instructions after the cursor's current position.</param>
        /// <returns></returns>
        public static IEnumerable MatchBefore(this ILCursor crs, Func<Instruction, bool> match, bool matchFromStart = true)
        {
            var curr = crs.Next;

            if (matchFromStart)
                crs.Index = 0;

            while (crs.JumpBeforeNext(match))
            {
                var c = crs.Next;

                yield return crs.Next;
                crs.Goto(c, MoveType.After);
            }

            crs.Next = curr;
        }

        /// <summary>
        /// Declares a new local in the given ILContext.
        /// </summary>
        /// <typeparam name="T">The type for the new local.</typeparam>
        /// <param name="ctx">The context to declare the local in.</param>
        /// <returns>The definition for the new local.</returns>
        public static VariableDefinition DeclareLocal<T>(this ILContext ctx)
        {
            var loc = new VariableDefinition(ctx.Import(typeof(T)));
            ctx.Body.Variables.Add(loc);

            return loc;
        }

        /// <summary>
        /// Declares a new local in the given ILCursor's context.
        /// </summary>
        /// <typeparam name="T">The type for the new local.</typeparam>
        /// <param name="ctx">The cursor to declare the local in.</param>
        /// <returns>The definition for the new local.</returns>
        public static VariableDefinition DeclareLocal<T>(this ILCursor crs)
        {
            return crs.Context.DeclareLocal<T>();
        }

        /// <summary>
        /// Moves after an instruction that will push an argument value for the given instruction.
        /// </summary>
        /// <param name="crs">The cursor to move.</param>
        /// <param name="targetInstr">The instruction that the move target will push an argument for.</param>
        /// <param name="argIndex">Which argument of the instruction should this move after?</param>
        /// <param name="instance">Which instance of the argument instrution should this move after? Only matters for branching arguments.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool TryGotoArg(this ILCursor crs, Instruction targetInstr, int argIndex, int instance = 0)
        {
            if (argIndex < 0)
                return false;

            if (instance < 0)
                return false;

            if (targetInstr == null)
                return false;

            var argumentInstrs = targetInstr.GetArgumentInstructions(crs.Context, argIndex);

            if (instance >= argumentInstrs.Count)
                return false;

            crs.Goto(argumentInstrs[instance], MoveType.After);
            return true;
        }

        /// <summary>
        /// Moves after an instruction that will push an argument value for the cursor's next instruction.
        /// </summary>
        /// <param name="crs">The cursor to move.</param>
        /// <param name="argIndex">Which argument of the instruction should this move after?</param>
        /// <param name="instance">Which instance of the argument instrution should this move after? Only matters for branching arguments.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool TryGotoArg(this ILCursor crs, int argIndex, int instance = 0)
        {
            return crs.TryGotoArg(crs.Next, argIndex, instance);
        }

        /// <summary>
        /// Moves after all instructions that will push an argument value for the given instruction.
        /// </summary>
        /// <param name="crs">The cursor to move.</param>
        /// <param name="targetInstr">The instruction that the move target will push an argument for.</param>
        /// <param name="argIndex">Which argument of the instruction should this move after?</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static IEnumerable MatchArg(this ILCursor crs, Instruction targetInstr, int argIndex)
        {
            if (argIndex < 0)
                yield break;

            if (targetInstr == null)
                yield break;

            var curr = crs.Next;
            var argumentInstrs = targetInstr.GetArgumentInstructions(crs.Context, argIndex);

            foreach (var arg in argumentInstrs)
            {
                crs.Goto(arg, MoveType.After);

                yield return null;
            }

            crs.Next = curr;
        }

        /// <summary>
        /// Moves after all instructions that will push an argument value for the cursor's next instruction.
        /// </summary>
        /// <param name="crs">The cursor to move.</param>
        /// <param name="argIndex">Which argument of the instruction should this move after?</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static IEnumerable MatchArg(this ILCursor crs, int argIndex)
        {
            return crs.MatchArg(crs.Next, argIndex);
        }

        private static List<Instruction> GetArgumentInstructions(this Instruction instruction, ILContext context, int argumentIndex)
        {
            var args = instruction.InputCount();
            var moves = args - argumentIndex - 1;

            if (moves < 0)
                return [];

            var prev = instruction.PossiblePreviousInstructions(context);
            var argInstrs = new List<Instruction>();

            foreach (var i in prev)
                BacktrackToArg(i, context, moves, argInstrs);

            argInstrs.Sort((a, b) => context.IndexOf(a).CompareTo(context.IndexOf(b)));

            return argInstrs;
        }

        private static void BacktrackToArg(Instruction current, ILContext ctx, int remainingMoves, List<Instruction> foundArgs)
        {
            if (remainingMoves <= 0 && current.OutputCount() > 0)
            {
                if (remainingMoves == 0)
                    foundArgs.Add(current);

                return;
            }

            remainingMoves -= current.StackDelta();
            var prev = current.PossiblePreviousInstructions(ctx);

            foreach (var i in prev)
                BacktrackToArg(i, ctx, remainingMoves, foundArgs);
        }

        /// <summary>
        /// Returns how many values the given instruction will pop from the stack.
        /// </summary>
        /// <param name="instr">The instruction to check.</param>
        /// <returns>How many values the given instruction will pop from the stack.</returns>
        public static int InputCount(this Instruction instr)
        {
            if (instr == null)
                return 0;

            var op = instr.OpCode;

            if (op.FlowControl == FlowControl.Call)
            {
                var mthd = (IMethodSignature)instr.Operand;
                var ins = 0;

                if (op.Code != Code.Newobj && mthd.HasThis && !mthd.ExplicitThis)
                    ins++; // Input the "self" arg

                if (mthd.HasParameters)
                    ins += mthd.Parameters.Count; // Input all of the parameters

                if (op.Code == Code.Calli)
                    ins++; // No clue for this one

                return ins;
            }

            return op.StackBehaviourPop switch
            {
                StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
                StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi or StackBehaviour.Popi_popi8 or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8 or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
                StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popi8 or StackBehaviour.Popref_popi_popr4 or StackBehaviour.Popref_popi_popr8 or StackBehaviour.Popref_popi_popref => 3,

                _ => 0,
            };
        }

        /// <summary>
        /// Returns how many values the given instruction will push to the stack.
        /// </summary>
        /// <param name="instr">The instruction to check.</param>
        /// <returns>How many values the given instruction will push to the stack.</returns>
        public static int OutputCount(this Instruction instr)
        {
            if (instr == null)
                return 0;

            var op = instr.OpCode;

            if (op.FlowControl == FlowControl.Call)
            {
                var mthd = (IMethodSignature)instr.Operand;
                var outs = 0;

                if (op.Code == Code.Newobj || mthd.ReturnType.MetadataType != MetadataType.Void)
                    outs++; // Output the return value

                return outs;
            }

            return op.StackBehaviourPush switch
            {
                StackBehaviour.Push1 or StackBehaviour.Pushi or StackBehaviour.Pushi8 or StackBehaviour.Pushr4 or StackBehaviour.Pushr8 or StackBehaviour.Pushref => 1,
                StackBehaviour.Push1_push1 => 2,

                _ => 0,
            };
        }

        /// <summary>
        /// Returns how much this instruction changes the stack size.
        /// </summary>
        /// <param name="instr">The instruction to check.</param>
        /// <returns>How much this instruction changes the stack size.</returns>
        public static int StackDelta(this Instruction instr)
        {
            return instr.OutputCount() - instr.InputCount();
        }

        /// <summary>
        /// Returns the possible instructions that can move to the given instruction.
        /// </summary>
        /// <param name="instr">The instruction to check.</param>
        /// <param name="ctx">The context that contains the given instruction.</param>
        /// <returns>A list of possible instructions that can move to the given instruction.</returns>
        public static List<Instruction> PossiblePreviousInstructions(this Instruction instr, ILContext ctx)
        {
            var l = new List<Instruction>();

            foreach (var i in ctx.Instrs)
            {
                if (Array.IndexOf(i.PossibleNextInstructions(), instr) >= 0)
                    l.Add(i);
            }

            return l;
        }

        /// <summary>
        /// Returns the possible instructions the given instruction can move to.
        /// </summary>
        /// <param name="instr">The instruction to check.</param>
        /// <returns>A list of possible instructions that can move to the given instruction.</returns>
        public static Instruction[] PossibleNextInstructions(this Instruction instr)
        {
            return instr.OpCode.FlowControl switch
            {
                FlowControl.Next or FlowControl.Call => [instr.Next],
                FlowControl.Branch => instr.GetBranchTarget() is Instruction tr ? [tr] : Array.Empty<Instruction>(),
                FlowControl.Cond_Branch => instr.GetBranchTarget() is Instruction tr ? [instr.Next, tr] : [instr.Next],

                _ => []
            };
        }

        /// <summary>
        /// Returns the given instruction's branch target as an Instruction. If the given instruction doesn't have a branch target, returns null.
        /// </summary>
        /// <param name="branch">The instruction to check.</param>
        /// <returns>The given instruction's branch target or null if there is none.</returns>
        public static Instruction GetBranchTarget(this Instruction branch)
        {
            if (branch.Operand is Instruction tr)
                return tr;

            if (branch.Operand is ILLabel lb)
                return lb.Target;

            return null;
        }

        /// <summary>
        /// Instruction.ToString() fixed to work with ILLabel branch targets.
        /// </summary>
        /// <param name="c">Target instruction.</param>
        /// <returns>The given instruction converted to a string.</returns>
        public static string InstructionToString(this Instruction c)
        {
            if (c == null)
                return "Null instruction";

            try
            {
                return c.ToString();
            }
            catch
            {
                try
                {
                    if (c.OpCode.OperandType is OperandType.InlineBrTarget or OperandType.ShortInlineBrTarget && c.Operand is ILLabel l)
                        return $"IL_{c.Offset:x4}: {c.OpCode.Name} IL_{l.Target.Offset:x4}";

                    if (c.OpCode.OperandType is OperandType.InlineSwitch && c.Operand is IEnumerable<ILLabel> en)
                        return $"IL_{c.Offset:x4}: {c.OpCode.Name} {string.Join(", ", en.Select(x => x.Target.Offset.ToString("x4")))}";
                }
                catch { }
            }

            return $"IL_{c.Offset:x4}: {c.OpCode.Name} (This shouldn't be happening)";
        }

        /// <summary>
        /// Gets the value of a compiler-generated IEnumerator's field.
        /// </summary>
        /// <typeparam name="T">The field's type.</typeparam>
        /// <param name="obj">The enumerator to get the field from.</param>
        /// <param name="name">The field's name.</param>
        /// <returns>The field's value.</returns>
        public static T EnumeratorGetField<T>(this object obj, string name) => (T)obj.GetType().EnumeratorField(name).GetValue(obj);

        /// <summary>
        /// Gets a FieldInfo from the given method's declaring IEnumerator type.
        /// </summary>
        /// <param name="method">The method to get the field from.</param>
        /// <param name="name">The field's name.</param>
        /// <returns>The field.</returns>
        public static FieldInfo EnumeratorField(this MethodBase method, string name) => method.DeclaringType.EnumeratorField(name);

        /// <summary>
        /// Gets a FieldInfo from the given IEnumerator type.
        /// </summary>
        /// <param name="tp">The IEnumerator type.</param>
        /// <param name="name">The field's name.</param>
        /// <returns>The field.</returns>
        public static FieldInfo EnumeratorField(this Type tp, string name) => tp.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).First(x => x != null && x.Name != null && (x.Name.Contains($"<{name}>") || x.Name == name));
    }
}
