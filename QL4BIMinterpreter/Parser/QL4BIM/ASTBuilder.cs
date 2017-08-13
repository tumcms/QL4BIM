﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace QL4BIMinterpreter.QL4BIM
{
    public class AstBuilder : IAstBuilder
    {
        public void RegisterParseEvent(Parser parser)
        {
            parser.PartParsed += ParserOnPartParsed;
            parser.ContextChanged += Parser_ContextChanged;
        }

        public FunctionNode Block { get; } //todo user func

        private ContextSwitch CurrentContextSwitch { get; set; }

        private const string GlobalFunctionId = "GlobalFunction";

        private void Parser_ContextChanged(object sender, ContextChangedEventArgs e)
        {
            Console.WriteLine(e.ToString());

            contextSwitch?.TearDownContext();

            switch (e.Context)
            {
                case ParserContext.GlobalBlock:
                    globalContextSwitch = new FunctionContextSwitch();
                    globalContextSwitch.AddNode(GlobalFunctionId, ParserParts.DefOp);
                    functionNode = globalContextSwitch.FunctionNode;
                    userFunctionNode = functionNode;
                    //currentFunctionNode = globalContextSwitch.FunctionNode; // todo set in func context
                    break;

                case ParserContext.UserFuncBlock:
                    contextSwitch = new FunctionContextSwitch();
                    break;

                case ParserContext.Statement:
                    contextSwitch = new StatementContextSwitch() {ParentFuncNode = userFunctionNode };
                    contextSwitch.AddNode("Statement", ParserParts.NullPart);
                    break;


                case ParserContext.Variable:
                    contextSwitch = new ParseVariableContextSwitch() {ParentFuncNode = userFunctionNode };
                    break;

                case ParserContext.Operator:
                    contextSwitch = new OperatorContextSwitch() {ParentFuncNode = userFunctionNode };
                    break;

                case ParserContext.Argument:
                    if (!(contextSwitch is ParseArgumentContextSwitch))
                        contextSwitch = new ParseArgumentContextSwitch() {ParentFuncNode = userFunctionNode };
                    break;

                case ParserContext.AttPredicate:
                    contextSwitch = new AttributePredicateSwitch() { ParentFuncNode = userFunctionNode };
                    break;

                case ParserContext.CountPredicate:
                    contextSwitch = new CountPredicateContextSwitch() { ParentFuncNode = userFunctionNode };
                    break;

                case ParserContext.ArgumentEnd:
                    //contextSwitch?.TearDownContext();
                    break;
            }
        }

        private void ParserOnPartParsed(object sender, PartParsedEventArgs e)
        {
            Console.WriteLine('\t' + e.ToString());
            contextSwitch.AddNode(e.CurrentToken, e.ParsePart);

        }


        private FunctionContextSwitch globalContextSwitch;


        private ContextSwitch contextSwitch
        {
            get { return contextSwitch1; }
            set
            {
                contextSwitch1 = value; 
                contextSwitch1.InitContext();
            }
        }

        private FunctionNode functionNode;
        private FunctionNode userFunctionNode;

        private ContextSwitch contextSwitch1;


        abstract class ContextSwitch
        {
            public FunctionNode ParentFuncNode { get; set; }

            public abstract void AddNode(string value, ParserParts parserPart);

            public abstract void TearDownContext();
            public virtual void InitContext() {}

            protected void AddArgument(Node node)
            {
                ParentFuncNode.LastStatement.Arguments.Add(node);
            }

            protected Node PopLastArguement()
            {
                var argCount = ParentFuncNode.LastStatement.Arguments.Count;
                var lastArg = ParentFuncNode.LastStatement.Arguments.LastOrDefault();

                if (lastArg == null)
                    throw new ArgumentException("Argument is missing");

                ParentFuncNode.LastStatement.Arguments.RemoveAt(argCount - 1);
                return lastArg;
            }

        }

        class FunctionContextSwitch : ContextSwitch
        {
               
            public FunctionNode FunctionNode { get; private set; }

            public override void AddNode(string value, ParserParts parserPart)
            {   
                if(GlobalFunctionId == value)
                    FunctionNode = new FunctionNode(value);
                else
                    FunctionNode.UserFunctions.Add(new UserFunctionNode(value));
            }

            public override void TearDownContext() {}


        }

        class StatementContextSwitch : ContextSwitch
        {
            public override void AddNode(string value, ParserParts parserPart)
            {
                ParentFuncNode.AddStatement(new StatementNode());
            }

            public override void TearDownContext() { }


        }

        class OperatorContextSwitch : ContextSwitch
        {
            public override void AddNode(string value, ParserParts parserPart)
            {
                if( parserPart != ParserParts.Operator)
                    throw new ArgumentOutOfRangeException(nameof(parserPart), parserPart, null);
                
                ParentFuncNode.LastStatement.SetOperator(value);
            }

            public override void TearDownContext() { }


        }

        class AttributePredicateSwitch : SetRelContextSwitch
        {
            public ParserParts Compare { get; set; }
            public AttributeAccessNode AttributeAccessNodeStart { get; set; }
            public Node AttributeAccessNodeEnd { get; set; }

            public override void AddNode(string value, ParserParts parserPart)
            {
                switch (parserPart)
                {   
                    case ParserParts.EqualsPred:
                        Compare = parserPart;
                        return;
                    case ParserParts.InPred:
                        Compare = parserPart;
                        return;
                    case ParserParts.MorePred:
                        Compare = parserPart;
                        return;
                    case ParserParts.MoreEqualPred:
                        Compare = parserPart;
                        return;
                    case ParserParts.LessPred:
                        Compare = parserPart;
                        return;
                    case ParserParts.LessEqualPred:
                        Compare = parserPart;
                        return;

                    case ParserParts.String:
                        AttributeAccessNodeEnd = new CStringNode(value);
                        return;
                    case ParserParts.Number:
                        AttributeAccessNodeEnd = new CNumberNode(value);
                        return;
                    case ParserParts.Float:
                        AttributeAccessNodeEnd = new CFloatNode(value);
                        return;
                    case ParserParts.Bool:
                        AttributeAccessNodeEnd = new CBoolNode(value);
                        return;

                    case ParserParts.SetRelArg: //ParserParts.SetRelArg
                        PrimeVariable = value;
                        break;
                    case ParserParts.RelAtt: //ParserParts.EmptyRelAtt
                        SecondaryVariable = value;
                        break;
                    case ParserParts.ExAtt:
                        AttributeAccessNodeEnd = DoExAtt(value);
                        break;
                }
            }

            public override void TearDownContext()
            {
                if (AttributeAccessNodeStart == null)
                    return;

                var predicate = new PredicateNode(AttributeAccessNodeStart, Compare, AttributeAccessNodeEnd);
                AddArgument(predicate);

                AttributeAccessNodeStart = null;
            }

            public override void InitContext()
            {
                var lastArguement = PopLastArguement();
                if (!(lastArguement is AttributeAccessNode))
                    throw new ArgumentException();

                AttributeAccessNodeStart = lastArguement as AttributeAccessNode;
            }
        }

        abstract class SetRelContextSwitch : ContextSwitch
        {
            protected string PrimeVariable;
            protected string SecondaryVariable;


            public override void TearDownContext()
            {
                PrimeVariable = null;
                SecondaryVariable = null;

            }

            protected AttributeAccessNode DoExAtt(string value)
            {
                AttributeAccessNode attributeAccess = null;
                if (PrimeVariable != null && SecondaryVariable == null)
                    attributeAccess = new AttributeAccessNode(new SetNode(PrimeVariable), new ExAttNode(value));
                else if (SecondaryVariable != null)
                    attributeAccess = new AttributeAccessNode(
                        new RelAttNode(SecondaryVariable, PrimeVariable), new ExAttNode(value));

                PrimeVariable = null;
                SecondaryVariable= null;

                return attributeAccess;
            }


        }

        class ParseVariableContextSwitch : ContextSwitch
        {
            private string primeVariable;
            private readonly List<string> secondaryVariable = new List<string>();

            public override void AddNode(string value, ParserParts parserPart)
            {
                switch (parserPart)
                {
                    case ParserParts.SetRelVar: 
                        primeVariable = value;
                        break;
                    case ParserParts.RelAtt: 
                        secondaryVariable.Add(value);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            public override void TearDownContext()
            {
                if (secondaryVariable.Count == 0)
                    ParentFuncNode.LastStatement.SetSetReturn(new SetNode(primeVariable));
                else
                {
                    var relation = new RelationNode(primeVariable);
                    relation.Attributes = secondaryVariable.ToList();
                    ParentFuncNode.LastStatement.SetRelationReturn(relation);
                }

                primeVariable = null;
                secondaryVariable.Clear();
            }

        }

        class ParseArgumentContextSwitch : SetRelContextSwitch
        {

            public override void AddNode(string value, ParserParts parserPart)
            {
                switch (parserPart)
                {
                    case ParserParts.SetRelArg: //ParserParts.SetRelArg
                        PrimeVariable = value;
                        break;
                    case ParserParts.RelAtt: //ParserParts.EmptyRelAtt
                        SecondaryVariable = value;

                        var predecessorArg = PopLastArguement();
                        if (predecessorArg == null)
                            throw new ArgumentException("a relational Attribute can only be used after a relational Argument.");

                        if (!String.IsNullOrEmpty(PrimeVariable) && PrimeVariable != predecessorArg.Value)     
                            throw new ArgumentException("a relational Attribute can only be used after a relational Argument with the same Name.");
                            
                        if (!(predecessorArg is SetNode))
                            throw new ArgumentException("a relational Attribute can only be used after a relational Argument.");
                            
                        var relNameNode = new RelNameNode(predecessorArg.Value);
                        AddArgument(relNameNode);

                        break;

                    case ParserParts.ExAtt:
                        AddArgument(DoExAtt(value));
                        break;

                    //Type Predicate
                    case ParserParts.ExType:
                        TypePredNode typePredNode = null;
                        if (PrimeVariable != null && SecondaryVariable == null)
                            typePredNode = new TypePredNode(new SetNode(PrimeVariable), value);
                        else if (SecondaryVariable != null)
                            typePredNode = new TypePredNode(
                                new RelAttNode(SecondaryVariable, PrimeVariable), value);
                        AddArgument(typePredNode);
                        PrimeVariable = null;
                        SecondaryVariable = null;
                        break;

   

                    //constants
                    case ParserParts.String: 
                        ParentFuncNode.LastStatement.AddArgument(new CStringNode(value));
                        break;
                    case ParserParts.Number: 
                        ParentFuncNode.LastStatement.AddArgument(new CNumberNode(value));
                        break;
                    case ParserParts.Float: 
                        ParentFuncNode.LastStatement.AddArgument(new CFloatNode(value));
                        break;
                    case ParserParts.Bool: 
                        ParentFuncNode.LastStatement.AddArgument(new CBoolNode(value));
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }


            public override void TearDownContext()
            {
                //if (PrimeVariable == null && SecondaryVariable.Count == 0)
                //throw  new ArgumentException("PrimeVariable = null and SecondaryVariable.Count = 0");

                //set
                if (PrimeVariable != null && SecondaryVariable == null)
                {
                    AddArgument(new SetNode(PrimeVariable));
                }

                //rel
                else if (SecondaryVariable != null)
                {
                    var relAttNode = new RelAttNode(SecondaryVariable, PrimeVariable);
                    AddArgument(relAttNode);
                }

                base.TearDownContext();

            }

        }

        class CountPredicateContextSwitch : ContextSwitch
        {
            private ParserParts compare;
            private RelAttNode relAttNode;
            private CNumberNode cNumberNode;

            public override void InitContext()
            {
                var predecessor = PopLastArguement();

                if(!(predecessor is RelAttNode))
                    throw  new ArgumentException("predecessor arguement must be a RelAtt.");

                relAttNode = predecessor as RelAttNode;
            }

            public override void AddNode(string value, ParserParts parserPart)
            {
                switch (parserPart)
                {
                    case ParserParts.EqualsPred:
                        compare = parserPart;
                        break;
                    case ParserParts.MorePred:
                        compare = parserPart;
                        break;
                    case ParserParts.MoreEqualPred:
                        compare = parserPart;
                        break;
                    case ParserParts.LessPred:
                        compare = parserPart;
                        break;
                    case ParserParts.LessEqualPred:
                        compare = parserPart;
                        break;
                    case ParserParts.Number:
                        cNumberNode = new CNumberNode(value);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            public override void TearDownContext()
            {   
                if(relAttNode == null)
                    return;

                AddArgument(new PredicateNode(relAttNode, compare, cNumberNode));
                relAttNode = null;
            }
        }
    }
}