// Copyright (c) Thomas Steinfeld. All rights reserved.
// This file is licensed under the Apache License, Version 2.0 
// See the license.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsBasic.Nodes;
using static TsBasic.ERuntimeErrors;

namespace TsBasic
{

	enum ERuntimeErrors
	{
		// Non fatal
		NonFatalStart = 1000,
		DivisionByZero,
		InvalidTab,

		FatalStart =5000,
		Unspecified,
		UnspecifiedWithInfo,
		CsException,
		NotImplemented,
		ReturnWithoutGosub,
		JumpToUndefinedLabel,
		JumpIntoForLoop,
		CallToUnknownFunction,
		ZeroStep,
		EndOfData,
		ConversionError,
		ArrayDefWithoutDimNotPossible,
		ArrayIndexOutOfRange,
		ArrayDimensionMismatch,
		OnGotoIndexInvalid,
		StackOverflow,
		InputArgumentInvalid,


	}

	public class RuntimeErrorNode : ControlNode
	{

		static Dictionary<ERuntimeErrors, string> errorDict;

		static object[] errMessages =
		{
			Unspecified, "no further information available",
			UnspecifiedWithInfo, "{0}",
			ReturnWithoutGosub, "RETURN without previous GOSUB",
			NotImplemented, "{0} not implemented",
			JumpToUndefinedLabel, "jump to undefined label {0}",
			JumpIntoForLoop, "jump into a for loop from outside",
			CallToUnknownFunction,"call to unknown function {0}",
			InvalidTab, "TAB({0}) is invalid",
			ZeroStep, "STEP 0 is not allowed, infinite loop",
			ArrayIndexOutOfRange,"array index out of range for array {0}",
			ArrayDimensionMismatch,	"array dimension mismatch",
			OnGotoIndexInvalid,"ON...GOTO called with invalid index {0}",
			EndOfData,"READ behind the end of available data",
			DivisionByZero,"division by zero",
			InputArgumentInvalid,"'{0}' is not a valid input value for variable '{1}'",
			CsException, "Ups! You crashed the interpreter!\r\nPlease send your program and the following information to the developer.\r\n{0}\r\nThank you!\r\n"

		};

		static RuntimeErrorNode()
		{
			errorDict = new Dictionary<ERuntimeErrors, string>();
			for (int i = 0; i < errMessages.Length; i += 2)
			{
				var errno = (ERuntimeErrors)errMessages[i];
				errorDict.Add(errno, errMessages[i + 1].ToString());
			}
		}

		public int Line;

		internal ERuntimeErrors ErrNo;

		object[] args;

		public string Message => GetMessage();

		public bool IsFatal => ErrNo > ERuntimeErrors.FatalStart;

		internal RuntimeErrorNode(int line, ERuntimeErrors error = ERuntimeErrors.Unspecified, params object[] args)
		{
			Kind = EvalResultKind.Error;
			Line = line;
			ErrNo = error;
			this.args = args;
		}

		string GetMessage()
		{
			string message;
			if (!errorDict.TryGetValue(ErrNo, out message))
				return ErrNo.ToString();
			return string.Format(message, args);
		}

		public override string ToString()
		{
			string s= $"untime error {(int)ErrNo} at line {Line}: {Message}";
			s = (IsFatal ? "Fatal r" : "R") + s;
			return s;
		}

	}
}
