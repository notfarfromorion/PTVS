﻿// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    public sealed class ExpressionFinder {
        public ExpressionFinder(PythonAst ast, GetExpressionOptions options) {
            Ast = ast;
            Options = options.Clone();
        }

        public PythonAst Ast { get; }
        public GetExpressionOptions Options { get; }

        public Node GetExpression(int index) {
            var walker = new ExpressionWalker(Ast, index, index, Options);
            Ast.Walk(walker);
            return walker.Expression;
        }

        public Node GetExpression(SourceLocation location) {
            int index = Ast.LocationToIndex(location);
            return GetExpression(index);
        }

        public SourceSpan? GetExpressionSpan(int index) {
            var walker = new ExpressionWalker(Ast, index, index, Options);
            Ast.Walk(walker);
            return walker.Expression?.GetSpan(Ast);
        }

        public SourceSpan? GetExpressionSpan(SourceLocation location) {
            int index = Ast.LocationToIndex(location);
            return GetExpressionSpan(index);
        }

        public Node GetExpression(int startIndex, int endIndex) {
            var walker = new ExpressionWalker(Ast, startIndex, endIndex, Options);
            Ast.Walk(walker);
            return walker.Expression;
        }

        public Node GetExpression(SourceSpan range) {
            int startIndex = Ast.LocationToIndex(range.Start);
            int endIndex = Ast.LocationToIndex(range.End);
            return GetExpression(startIndex, endIndex);
        }

        public SourceSpan? GetExpressionSpan(int startIndex, int endIndex) {
            var walker = new ExpressionWalker(Ast, startIndex, endIndex, Options);
            Ast.Walk(walker);
            return walker.Expression?.GetSpan(Ast);
        }

        public SourceSpan? GetExpressionSpan(SourceSpan range) {
            int startIndex = Ast.LocationToIndex(range.Start);
            int endIndex = Ast.LocationToIndex(range.End);
            return GetExpressionSpan(startIndex, endIndex);
        }

        private class ExpressionWalker : PythonWalkerWithLocation {
            private readonly int _endLocation;
            private readonly PythonAst _ast;
            private readonly GetExpressionOptions _options;

            public ExpressionWalker(PythonAst ast, int location, int endLocation, GetExpressionOptions options) : base(location) {
                _ast = ast;
                _endLocation = endLocation;
                _options = options;
                Expression = null;
            }

            public Node Expression { get; private set; }

            private bool Save(Node node, bool baseWalk, bool ifTrue) {
                if (baseWalk && !(node.StartIndex <= _endLocation && _endLocation <= node.EndIndex)) {
                    return false;
                }

                if (baseWalk && ifTrue) {
                    Expression = node;
                }
                return baseWalk;
            }

            private bool BeforeBody(Node body) {
                if (Location >= body.StartIndex) {
                    return false;
                }

                var ws = body.GetLeadingWhiteSpace(_ast);
                if (string.IsNullOrEmpty(ws)) {
                    return false;
                }

                if (Location >= body.StartIndex - ws.Length) {
                    return false;
                }

                return true;
            }

            public override bool Walk(CallExpression node) => Save(node, base.Walk(node), _options.Calls);
            public override bool Walk(ConstantExpression node) => Save(node, base.Walk(node), _options.Literals);
            public override bool Walk(NameExpression node) => Save(node, base.Walk(node), _options.Names);
            public override bool Walk(Parameter node) => Save(node, base.Walk(node), _options.ParameterNames && Location <= node.StartIndex + node.Name.Length);
            public override void PostWalk(ClassDefinition node) => Save(node, true, _options.ClassDefinition && BeforeBody(node.Body));
            public override void PostWalk(FunctionDefinition node) => Save(node, true, _options.FunctionDefinition && BeforeBody(node.Body));

            public override bool Walk(MemberExpression node) {
                if (Save(node, base.Walk(node), _options.Members && Location >= node.NameHeader)) {
                    if (_options.MemberName) {
                        var nameNode = new NameExpression(node.Name);
                        nameNode.SetLoc(node.NameHeader, node.EndIndex);
                        Expression = nameNode;
                    }
                    return true;
                }
                return false;
            }
        }
    }

    public sealed class GetExpressionOptions {
        public static GetExpressionOptions Hover => new GetExpressionOptions();
        public static GetExpressionOptions Evaluate => new GetExpressionOptions {
            MemberName = false,
            ParameterNames = false,
            ClassDefinition = true,
            FunctionDefinition = true
        };

        public bool Calls { get; set; } = true;
        public bool Names { get; set; } = true;
        public bool Members { get; set; } = true;
        public bool MemberName { get; set; } = true;
        public bool Literals { get; set; } = true;
        public bool ParameterNames { get; set; } = true;
        public bool ClassDefinition { get; set; } = false;
        public bool FunctionDefinition { get; set; } = false;

        public GetExpressionOptions Clone() {
            return (GetExpressionOptions)MemberwiseClone();
        }
    }
}
