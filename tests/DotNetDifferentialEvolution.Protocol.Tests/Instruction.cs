using System.Reflection;
using System.Reflection.Emit;

namespace ProtocolChecks;

/// <summary>One instruction of a method body: the opcode and the member its metadata token names, when it names one.</summary>
internal readonly record struct Instruction(OpCode Code, MemberInfo? Operand);
