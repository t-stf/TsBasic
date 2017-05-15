using System.Collections.Generic; 
using System.Text;
using TsBasic.Nodes;



using System;

namespace TsBasic {



internal partial class Parser 
{

	public const int _EOF = 0;
	public const int _ident = 1;
	public const int _label_def = 2;
	public const int _integer = 3;
	public const int _float_1 = 4;
	public const int _float_2 = 5;
	public const int _float_3 = 6;
	public const int _stringToken = 7;
	public const int _eol = 8;
	public const int _stringIdent = 9;
	public const int _signToken = 10;
	public const int maxT = 52;



public BasicProgram prog ;
	


	
	void Get () {
		for (;;) {
			t = la;
			la = scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }

			la = t;
		}
	}

	
	void RusBas() {
		prog = new BasicProgram(this);
		
		Line();
		while (StartOf(1)) {
			Line();
		}
	}

	void Line() {
		StatementNode snode=null;
		string label=null;
		
		if (la.kind == 2 || la.kind == 3) {
			LineNumber(ref label);
		}
		if (la.kind == 30) {
			RemStatement(ref snode);
		} else if (StartOf(2)) {
			if (StartOf(3)) {
				Statement(ref snode);
			}
			Expect(8);
		} else SynErr(53);
		if (snode!=null)
		{
		var lnode=new LineNode(label,snode);
		prog.AddLine(lnode);
		}
		
	}

	void LineNumber(ref string label) {
		if (la.kind == 3) {
			Get();
			label=t.val; 
		} else if (la.kind == 2) {
			Get();
			label=t.val.Substring(0,t.val.Length-1) ; 
		} else SynErr(54);
	}

	void RemStatement(ref StatementNode node) {
		var sb=new StringBuilder();
		
		Expect(30);
		while (StartOf(4)) {
			Get();
			sb.Append(t.val); sb.Append(' '); 
		}
		Expect(8);
		node=new RemStatementNode(sb.ToString()); 
	}

	void Statement(ref StatementNode node) {
		var stoken=t;  
		switch (la.kind) {
		case 11: case 12: {
			EndStatement(ref node);
			break;
		}
		case 46: {
			OptionStatement(ref node);
			break;
		}
		case 36: {
			ReturnStatement(ref node);
			break;
		}
		case 37: {
			IfThenStatement(ref node);
			break;
		}
		case 39: {
			ForStatement(ref node);
			break;
		}
		case 41: {
			NextStatement(ref node);
			break;
		}
		case 44: {
			RestoreStatement(ref node);
			break;
		}
		case 1: case 9: case 29: {
			LetStatement(ref node);
			break;
		}
		case 42: {
			DataStatement(ref node);
			break;
		}
		case 13: {
			PrintStatement(ref node);
			break;
		}
		case 43: {
			ReadStatement(ref node);
			break;
		}
		case 31: case 32: case 33: {
			GotoOrSubStatement(ref node);
			break;
		}
		case 45: {
			DimStatement(ref node);
			break;
		}
		case 48: {
			OnGotoStatement(ref node);
			break;
		}
		case 49: {
			SimpleStatement(ref node);
			break;
		}
		case 50: {
			DefStatement(ref node);
			break;
		}
		case 51: {
			InputStatement(ref node);
			break;
		}
		default: SynErr(55); break;
		}
		if (node!=null)
		{
		node.Line=stoken.line; 
		node.Col=stoken.col;
		} 
	}

	void EndStatement(ref StatementNode node) {
		if (la.kind == 11) {
			Get();
		} else if (la.kind == 12) {
			Get();
		} else SynErr(56);
		node = new EndStatementNode(); 
	}

	void OptionStatement(ref StatementNode node) {
		string value=null;
		
		Expect(46);
		Expect(47);
		Expect(3);
		value=t.val; 
		if (value!="0" && value!="1")
		SemErr("base must be 0 or 1");
		node=new OptionStatement("base",value);
		
	}

	void ReturnStatement(ref StatementNode node) {
		Expect(36);
		node= new ReturnStatementNode();
	}

	void IfThenStatement(ref StatementNode node) {
		ExpressionNode boolExpr=null;
		string label=null;
		
		Expect(37);
		RelationalExpression(ref boolExpr);
		Expect(38);
		JumpLabel(ref label);
		node=new IfThenStatementNode(boolExpr,label); 
	}

	void ForStatement(ref StatementNode node) {
		ExpressionNode varExpr =null, startExpr=null, limitExpr=null, incrExpr = ConstantNode.One;
		
		Expect(39);
		NumericVariable(ref varExpr);
		prog.CheckForVarName(varExpr);
		Expect(23);
		NumericExpression(ref startExpr);
		Expect(34);
		NumericExpression(ref limitExpr);
		if (la.kind == 40) {
			Get();
			NumericExpression(ref incrExpr);
		}
		var fnode=new ForStatementNode(varExpr,startExpr,limitExpr, incrExpr);
		prog.PushFor(fnode);
		node=fnode;
		
	}

	void NextStatement(ref StatementNode node) {
		ExpressionNode varExpr=null;
		
		Expect(41);
		NumericVariable(ref varExpr);
		var nnode=new NextStatementNode(varExpr);
		prog.PopNext(ref nnode);
		node=nnode;
		
	}

	void RestoreStatement(ref StatementNode node) {
		Expect(44);
		node = new RestoreStatementNode(); 
	}

	void LetStatement(ref StatementNode node) {
		ExpressionNode vnode=null;
		ExpressionNode enode=null;
		
		if (la.kind == 29) {
			Get();
		}
		if (la.kind == 1) {
			NumericReference(ref vnode);
			Expect(23);
			NumericExpression(ref enode);
		} else if (la.kind == 9) {
			StringVariable(ref vnode);
			Expect(23);
			StringExpression(ref enode);
		} else SynErr(57);
		node=new LetStatementNode((ReferenceNode) vnode,enode);
		
	}

	void DataStatement(ref StatementNode node) {
		node= new NopStatementNode();
		var list = new List<ConstantNode>();
		
		Expect(42);
		DataList(list);
		prog.AddData(list);
		
	}

	void PrintStatement(ref StatementNode node) {
		var psn=new PrintStatementNode(); node=psn; 
		Expect(13);
		while (StartOf(5)) {
			PrintList(psn.items);
		}
	}

	void ReadStatement(ref StatementNode node) {
		var list=new List<ReferenceNode>(); 
		
		Expect(43);
		VariableList(list);
		node=new ReadStatementNode(list);
		
	}

	void GotoOrSubStatement(ref StatementNode node) {
		string label=null;
		bool isSub=false;
		
		if (la.kind == 31) {
			Get();
		} else if (la.kind == 32) {
			Get();
			isSub=true;
		} else if (la.kind == 33) {
			Get();
			if (la.kind == 34) {
				Get();
			} else if (la.kind == 35) {
				Get();
				isSub=true;
			} else SynErr(58);
		} else SynErr(59);
		JumpLabel(ref label);
		node=new JumpStatementNode(label, isSub); 
	}

	void DimStatement(ref StatementNode node) {
		var list= new List<StatementNode>();
		StatementNode stmt=null;
		
		Expect(45);
		DimContent(ref stmt);
		list.Add(stmt); 
		while (la.kind == 15) {
			Get();
			DimContent(ref stmt);
			list.Add(stmt); 
		}
		if (list.Count>1)
		node=new StatementSequenceNode(list);
		else
		node=list[0];
		
	}

	void OnGotoStatement(ref StatementNode node) {
		ExpressionNode nexpr=null;
		string label=null;
		var list=new List<string>();
		
		Expect(48);
		NumericExpression(ref nexpr);
		if (la.kind == 31) {
			Get();
		} else if (la.kind == 33) {
			Get();
			Expect(34);
		} else SynErr(60);
		JumpLabel(ref label);
		list.Add(label);
		while (la.kind == 15) {
			Get();
			JumpLabel(ref label);
			list.Add(label);
		}
		node=new OnGotoStatementNode(nexpr,list);
		
	}

	void SimpleStatement(ref StatementNode node) {
		
		Expect(49);
		node= new SimpleStatementNode(t.val);
	}

	void DefStatement(ref StatementNode node) {
		string name  =null;
		ExpressionNode expr=null;
		var list = new List<string>();
		
		Expect(50);
		Expect(1);
		name= t.val; 
		while (la.kind == 17) {
			FormalArgumentList(list);
		}
		Expect(23);
		NumericExpression(ref expr);
		node= new DefStatementNode(name,list,expr);
		
		
	}

	void InputStatement(ref StatementNode node) {
		var list=new 	 List<ReferenceNode>();
		ExpressionNode snode =null;
		
		Expect(51);
		while (la.kind == 7) {
			StringConstant(ref snode);
		}
		VariableList(list );
		var scnode= snode as ConstantNode;
		node= new InputStatementNode(scnode?.StringValue,list);
		
	}

	void PrintList(List<object> objects ) {
		ExpressionNode node=null;
		if (la.kind == 14 || la.kind == 15) {
			if (la.kind == 14) {
				Get();
			} else {
				Get();
			}
			objects.Add(t.val); 
			while (la.kind == 14 || la.kind == 15) {
				if (la.kind == 14) {
					Get();
				} else {
					Get();
				}
				objects.Add(t.val); 
			}
			while (StartOf(5)) {
				PrintList(objects);
			}
		} else if (StartOf(6)) {
			PrintItem(ref node);
			objects.Add(node); 
		} else SynErr(61);
	}

	void PrintItem(ref ExpressionNode node) {
		if (la.kind == 16) {
			TabCall(ref node);
		} else if (StartOf(7)) {
			Expression(ref node);
		} else SynErr(62);
	}

	void TabCall(ref ExpressionNode node) {
		ExpressionNode expr=null;
		
		Expect(16);
		Expect(17);
		NumericExpression(ref expr);
		node=new FuncExpressionNode("TAB",expr); 
		Expect(18);
	}

	void Expression(ref ExpressionNode node) {
		if (StartOf(8)) {
			NumericExpression(ref node);
		} else if (la.kind == 7 || la.kind == 9) {
			StringExpression(ref node);
		} else SynErr(63);
	}

	void NumericExpression(ref ExpressionNode node) {
		var list= new List<object>();
		ExpressionNode termNode=null;
		
		if (la.kind == 10) {
			Get();
			list.Add(t.val);
		}
		Term(ref termNode);
		list.Add(termNode); 
		while (LaIsSign) {
			Expect(10);
			list.Add(t.val);
			Term(ref termNode);
			list.Add(termNode); 
		}
		node= list.Count==1 ? list[0] as ExpressionNode : new AddSequenceExpressionNode(list); 
	}

	void StringExpression(ref ExpressionNode node) {
		if (la.kind == 7) {
			StringConstant(ref node);
		} else if (la.kind == 9) {
			StringVariable(ref node);
		} else SynErr(64);
	}

	void Term(ref ExpressionNode node) {
		var list= new List<object>();
		ExpressionNode eNode=null;
		
		Factor(ref eNode);
		list.Add(eNode); 
		while (la.kind == 19 || la.kind == 20) {
			if (la.kind == 19) {
				Get();
			} else {
				Get();
			}
			list.Add(t.val);
			Factor(ref eNode);
			list.Add(eNode); 
		}
		node= list.Count==1 ? list[0] as ExpressionNode : new MulSequenceExpressionNode(list); 
	}

	void Factor(ref ExpressionNode node) {
		var list= new List<object>();
		ExpressionNode eNode=null;
		
		PrimaryExpression(ref eNode);
		list.Add(eNode); 
		while (la.kind == 21 || la.kind == 22) {
			if (la.kind == 21) {
				Get();
			} else {
				Get();
			}
			PrimaryExpression(ref eNode);
			list.Add(eNode); 
		}
		node= list.Count==1 ? list[0] as ExpressionNode : new PowSequenceExpressionNode(list); 
	}

	void PrimaryExpression(ref ExpressionNode node) {
		if (la.kind == 17) {
			Get();
			NumericExpression(ref node);
			Expect(18);
		} else if (StartOf(9)) {
			NumericConstant(ref node);
		} else if (la.kind == 1) {
			NumericReference(ref node);
		} else SynErr(65);
	}

	void NumericConstant(ref ExpressionNode node) {
		string s = null; 
		NumericConstantString(ref s);
		node= NumericConstantNode.Create(this,s); 
	}

	void NumericReference(ref ExpressionNode node) {
		string functor=null;
		var list=new List<ExpressionNode>();
		ExpressionNode eNode=null;
		
		Expect(1);
		functor=t.val;
		while (la.val == "(") {
			Expect(17);
			if (StartOf(7)) {
				Expression(ref eNode);
				list.Add(eNode); 
			}
			while (la.kind == 15) {
				Get();
				Expression(ref eNode);
				list.Add(eNode); 
			}
			Expect(18);
		}
		node=new ReferenceNode(functor,list); 
	}

	void Reference(ref ExpressionNode node) {
		
		if (la.kind == 9) {
			StringVariable(ref node);
		} else if (la.kind == 1) {
			NumericReference(ref node);
		} else SynErr(66);
	}

	void StringVariable(ref ExpressionNode node) {
		Expect(9);
		node=new ReferenceNode(t.val); 
	}

	void NumericConstantString(ref string s) {
		if (la.kind == 3) {
			Get();
		} else if (la.kind == 4) {
			Get();
		} else if (la.kind == 5) {
			Get();
		} else if (la.kind == 6) {
			Get();
		} else SynErr(67);
		s= t.val; 
	}

	void StringConstant(ref ExpressionNode snode) {
		Expect(7);
		var s=t.val.Substring(1,t.val.Length-2); snode=new StringConstantNode(s);  
	}

	void RelationalExpression(ref ExpressionNode node) {
		ExpressionNode lhs=null, rhs=null;
		string functor=null;
		
		Expression(ref lhs);
		switch (la.kind) {
		case 23: {
			Get();
			break;
		}
		case 24: {
			Get();
			break;
		}
		case 25: {
			Get();
			break;
		}
		case 26: {
			Get();
			break;
		}
		case 27: {
			Get();
			break;
		}
		case 28: {
			Get();
			break;
		}
		default: SynErr(68); break;
		}
		functor=t.val; 
		Expression(ref rhs);
		node=new ReferenceNode(functor,lhs,rhs);
		
	}

	void Variable(ref ExpressionNode node) {
		if (la.kind == 9) {
			StringVariable(ref node);
		} else if (la.kind == 1) {
			NumericVariable(ref node);
		} else SynErr(69);
	}

	void NumericVariable(ref ExpressionNode node) {
		Expect(1);
		node=new ReferenceNode(t.val); 
	}

	void JumpLabel(ref string label) {
		if (la.kind == 1) {
			Get();
		} else if (la.kind == 3) {
			Get();
		} else SynErr(70);
		label=t.val; 
	}

	void DataList(List<ConstantNode> list ) {
		ConstantNode cnode =null;
		
		
		Datum(ref cnode);
		list.Add(cnode); 
		while (la.kind == 15) {
			Get();
			Datum(ref cnode);
			list.Add(cnode); 
		}
	}

	void Datum(ref ConstantNode node) {
		ExpressionNode enode=null;
		string s=null, signString="";
		
		
		if (la.kind == 7) {
			StringConstant(ref enode);
			node= enode as ConstantNode; 
		} else if (la.kind == 1) {
			Get();
			node=new StringConstantNode(t.val); 
		} else if (StartOf(10)) {
			if (LaIsSign) {
				Expect(10);
				signString=t.val; 
			}
			NumericConstantString(ref s);
			node=  NumericConstantNode.Create(this,signString+s) ; 
		} else SynErr(71);
	}

	void VariableList(List<ReferenceNode> list ) {
		ExpressionNode enode=null;
		
		Reference(ref enode);
		list.Add(enode as ReferenceNode); 
		while (la.kind == 15) {
			Get();
			Reference(ref enode);
			list.Add(enode as ReferenceNode); 
		}
	}

	void DimContent(ref StatementNode node) {
		ExpressionNode expr=null;
		ExpressionNode enode=null;
		var list=new List<ExpressionNode>();
		
		
		Variable(ref enode);
		Expect(17);
		NumericExpression(ref expr);
		list.Add(expr); 
		while (la.kind == 15) {
			Get();
			NumericExpression(ref expr);
			list.Add(expr); 
		}
		Expect(18);
		node=new DimStatementNode(enode, list);
		
	}

	void FormalArgumentList(List<string> list ) {
		ExpressionNode varNode=null;
		
		Expect(17);
		while (la.kind == 1 || la.kind == 9) {
			Variable(ref varNode);
			list.Add((varNode as ReferenceNode).Name);
			while (la.kind == 15) {
				Get();
				Variable(ref varNode);
				list.Add((varNode as ReferenceNode).Name);
			}
		}
		Expect(18);
	}



	public void Parse() {
		la = new Token();
		la.val = "";		
		Get();
		RusBas();
		Expect(0);

	}
	
	static readonly bool[,] set = {
		{_T,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x},
		{_x,_T,_T,_T, _x,_x,_x,_x, _T,_T,_x,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_T,_T,_T, _T,_T,_x,_x, _T,_T,_x,_T, _x,_T,_T,_T, _T,_T,_T,_x, _T,_T,_T,_T, _x,_x},
		{_x,_T,_x,_x, _x,_x,_x,_x, _T,_T,_x,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_T,_x,_T, _T,_T,_x,_x, _T,_T,_x,_T, _x,_T,_T,_T, _T,_T,_T,_x, _T,_T,_T,_T, _x,_x},
		{_x,_T,_x,_x, _x,_x,_x,_x, _x,_T,_x,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_T,_x,_T, _T,_T,_x,_x, _T,_T,_x,_T, _x,_T,_T,_T, _T,_T,_T,_x, _T,_T,_T,_T, _x,_x},
		{_x,_T,_T,_T, _T,_T,_T,_T, _x,_T,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _T,_x},
		{_x,_T,_x,_T, _T,_T,_T,_T, _x,_T,_T,_x, _x,_x,_T,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x},
		{_x,_T,_x,_T, _T,_T,_T,_T, _x,_T,_T,_x, _x,_x,_x,_x, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x},
		{_x,_T,_x,_T, _T,_T,_T,_T, _x,_T,_T,_x, _x,_x,_x,_x, _x,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x},
		{_x,_T,_x,_T, _T,_T,_T,_x, _x,_x,_T,_x, _x,_x,_x,_x, _x,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x},
		{_x,_x,_x,_T, _T,_T,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x},
		{_x,_x,_x,_T, _T,_T,_T,_x, _x,_x,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x}

	};
} // end Parser


public partial class Errors {

	public virtual string GetSynErrString (int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "ident expected"; break;
			case 2: s = "label_def expected"; break;
			case 3: s = "integer expected"; break;
			case 4: s = "float_1 expected"; break;
			case 5: s = "float_2 expected"; break;
			case 6: s = "float_3 expected"; break;
			case 7: s = "stringToken expected"; break;
			case 8: s = "eol expected"; break;
			case 9: s = "stringIdent expected"; break;
			case 10: s = "signToken expected"; break;
			case 11: s = "\"end\" expected"; break;
			case 12: s = "\"stop\" expected"; break;
			case 13: s = "\"print\" expected"; break;
			case 14: s = "\";\" expected"; break;
			case 15: s = "\",\" expected"; break;
			case 16: s = "\"tab\" expected"; break;
			case 17: s = "\"(\" expected"; break;
			case 18: s = "\")\" expected"; break;
			case 19: s = "\"*\" expected"; break;
			case 20: s = "\"/\" expected"; break;
			case 21: s = "\"^\" expected"; break;
			case 22: s = "\"**\" expected"; break;
			case 23: s = "\"=\" expected"; break;
			case 24: s = "\"<>\" expected"; break;
			case 25: s = "\"<=\" expected"; break;
			case 26: s = "\">=\" expected"; break;
			case 27: s = "\"<\" expected"; break;
			case 28: s = "\">\" expected"; break;
			case 29: s = "\"let\" expected"; break;
			case 30: s = "\"rem\" expected"; break;
			case 31: s = "\"goto\" expected"; break;
			case 32: s = "\"gosub\" expected"; break;
			case 33: s = "\"go\" expected"; break;
			case 34: s = "\"to\" expected"; break;
			case 35: s = "\"sub\" expected"; break;
			case 36: s = "\"return\" expected"; break;
			case 37: s = "\"if\" expected"; break;
			case 38: s = "\"then\" expected"; break;
			case 39: s = "\"for\" expected"; break;
			case 40: s = "\"step\" expected"; break;
			case 41: s = "\"next\" expected"; break;
			case 42: s = "\"data\" expected"; break;
			case 43: s = "\"read\" expected"; break;
			case 44: s = "\"restore\" expected"; break;
			case 45: s = "\"dim\" expected"; break;
			case 46: s = "\"option\" expected"; break;
			case 47: s = "\"base\" expected"; break;
			case 48: s = "\"on\" expected"; break;
			case 49: s = "\"randomize\" expected"; break;
			case 50: s = "\"def\" expected"; break;
			case 51: s = "\"input\" expected"; break;
			case 52: s = "??? expected"; break;
			case 53: s = "invalid Line"; break;
			case 54: s = "invalid LineNumber"; break;
			case 55: s = "invalid Statement"; break;
			case 56: s = "invalid EndStatement"; break;
			case 57: s = "invalid LetStatement"; break;
			case 58: s = "invalid GotoOrSubStatement"; break;
			case 59: s = "invalid GotoOrSubStatement"; break;
			case 60: s = "invalid OnGotoStatement"; break;
			case 61: s = "invalid PrintList"; break;
			case 62: s = "invalid PrintItem"; break;
			case 63: s = "invalid Expression"; break;
			case 64: s = "invalid StringExpression"; break;
			case 65: s = "invalid PrimaryExpression"; break;
			case 66: s = "invalid Reference"; break;
			case 67: s = "invalid NumericConstantString"; break;
			case 68: s = "invalid RelationalExpression"; break;
			case 69: s = "invalid Variable"; break;
			case 70: s = "invalid JumpLabel"; break;
			case 71: s = "invalid Datum"; break;

			default: s = "error " + n; break;
		}
		return s;
	}
} // Errors


public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}
}