// Copyright (c) Thomas Steinfeld. All rights reserved.
// This file is licensed under the Apache License, Version 2.0 
// See the license.txt file in the project root for more information.

using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using TsBasic.Nodes;

namespace TsBasic
{
	public enum DebuggerState
	{
		Starting,
		Running,
		Breaked,

		CompletedOK,
		CompletedWithError,
	}

	public class AsyncBasicRunner
	{
		public readonly BasicEnvironment Runtime;

		CancellationTokenSource source;

		public DebuggerState State;

		HashSet<int> breakLines = new HashSet<int>();

		public bool IgnoreNextBreakpoint { get; set; }

		public BasicNode Result = null;

		public AsyncBasicRunner(BasicEnvironment runtime)
		{
			this.Runtime = runtime;
		}

		public void SetBreakpoints(List<int> lineNumbers)
		{
			breakLines = new HashSet<int>(lineNumbers);
		}

		private bool InternalStepOver()
		{
			if (breakLines.Contains(Runtime.NextLineToExecute))
			{
				if (!IgnoreNextBreakpoint)
				{
					//BreakpointEncountered?.Invoke(this, Runtime.NextLineToExecute);
					State = DebuggerState.Breaked;
					return false;
				}
			}
			IgnoreNextBreakpoint = false;
			Result = Runtime.Step();
			if (Runtime.HasEnded || Result.IsAbort())
				State = DebuggerState.CompletedOK;
			if (Runtime.LastError != null)
				State = DebuggerState.CompletedWithError;
			if (State >= DebuggerState.CompletedOK)
			{
				//	Runtime.WriteRunCompleteMessage(watch, Result);
				//ExecutionComplete?.Invoke(this);
			}
			return State < DebuggerState.CompletedOK;
		}

		public int StepOver()
		{
			if (State >= DebuggerState.CompletedOK || !InternalStepOver())
				return -1;
			return Runtime.NextLineToExecute;
		}


		public async Task RunAsync()
		{
			source = new CancellationTokenSource();
			var task = Task.Run(() => RunFunc(source.Token), source.Token);
			await task;
		}

		void RunFunc(CancellationToken ctoken)
		{
			if (State >= DebuggerState.CompletedOK || State == DebuggerState.Running)
				return;
			if (State == DebuggerState.Starting)
				Runtime.Host.InitRun(Runtime);
			State = DebuggerState.Running;
			while (InternalStepOver())
			{
				if (ctoken.IsCancellationRequested)
				{
					State = DebuggerState.Breaked;
					ctoken.ThrowIfCancellationRequested();
				}
			}
		}

		public void Break()
		{
			source.Cancel();
			source.Token.WaitHandle.WaitOne();
		}
	}

}
