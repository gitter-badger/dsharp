// ScriptGenerator.cs
// Script#/Core/Compiler
// This source code is subject to terms and conditions of the Apache License, Version 2.0.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using ScriptSharp;
using ScriptSharp.ScriptModel;

namespace ScriptSharp.Generator {

    internal sealed class ScriptGenerator {

        private ScriptTextWriter _writer;
        private CompilerOptions _options;
        private SymbolSet _symbols;

        public ScriptGenerator(TextWriter writer, CompilerOptions options, SymbolSet symbols) {
            Debug.Assert(writer != null);
            _writer = new ScriptTextWriter(writer);

            _options = options;
            _symbols = symbols;
        }

        public CompilerOptions Options {
            get {
                return _options;
            }
        }

        public ScriptTextWriter Writer {
            get {
                return _writer;
            }
        }

        public void GenerateScript(SymbolSet symbolSet) {
            Debug.Assert(symbolSet != null);

            List<TypeSymbol> types = new List<TypeSymbol>();
            List<TypeSymbol> publicTypes = new List<TypeSymbol>();
            List<TypeSymbol> internalTypes = new List<TypeSymbol>();

            foreach (NamespaceSymbol namespaceSymbol in symbolSet.Namespaces) {
                if (namespaceSymbol.HasApplicationTypes) {
                    foreach (TypeSymbol type in namespaceSymbol.Types) {
                        if (type.IsApplicationType == false) {
                            continue;
                        }

                        if (type.Type == SymbolType.Delegate) {
                            // Nothing needs to be generated for delegate types.
                            continue;
                        }

                        if (type.IsTestType && (_options.IncludeTests == false)) {
                            continue;
                        }

                        if ((type.Type == SymbolType.Enumeration) && (type.IsPublic == false)) {
                            // Internal enums can be skipped since their values have been inlined.
                            continue;
                        }

                        types.Add(type);
                        if (type.IsPublic) {
                            publicTypes.Add(type);
                        }
                        else {
                            internalTypes.Add(type);
                        }
                    }
                }
            }

            // Sort the types, so similar types of types are grouped, and parent classes
            // come before derived classes.
            types.Sort(new TypeComparer());

            string moduleName = symbolSet.ScriptName;

            _writer.Write("define('");
            _writer.Write(moduleName);
            _writer.Write("', [");

            bool firstDependency = true;
            foreach (string dependency in _symbols.Dependencies) {
                if (firstDependency == false) {
                    _writer.Write(", ");
                }
                _writer.Write("'" + dependency + "'");
                firstDependency = false;
            }

            _writer.Write("], function(");

            firstDependency = true;
            foreach (string dependency in _symbols.Dependencies) {
                if (firstDependency == false) {
                    _writer.Write(", ");
                }
                _writer.Write(dependency);
                firstDependency = false;
            }

            _writer.WriteLine(") {");
            _writer.Indent++;
            _writer.WriteLine("\'use strict';");
            _writer.WriteLine();

            foreach (TypeSymbol type in types) {
                TypeGenerator.GenerateScript(this, type);
            }

            _writer.Write("var $" + moduleName + " = ss.module('");
            _writer.Write(symbolSet.ScriptName);
            _writer.Write("',");
            if (internalTypes.Count != 0) {
                _writer.WriteLine();
                _writer.Indent++;
                _writer.WriteLine("{");
                _writer.Indent++;
                bool firstType = true;
                foreach (TypeSymbol type in types) {
                    if (type.IsPublic == false) {
                        if ((type.Type == SymbolType.Class) &&
                            ((ClassSymbol)type).HasGlobalMethods) {
                            continue;
                        }

                        if (firstType == false) {
                            _writer.WriteLine(",");
                        }
                        TypeGenerator.GenerateRegistrationScript(this, type);
                        firstType = false;
                    }
                }
                _writer.Indent--;
                _writer.WriteLine();
                _writer.Write("},");
                _writer.Indent--;
            }
            else {
                _writer.Write(" null,");
            }
            if (publicTypes.Count != 0) {
                _writer.WriteLine();
                _writer.Indent++;
                _writer.WriteLine("{");
                _writer.Indent++;
                bool firstType = true;
                foreach (TypeSymbol type in types) {
                    if (type.IsPublic) {
                        if ((type.Type == SymbolType.Class) &&
                            ((ClassSymbol)type).HasGlobalMethods) {
                            continue;
                        }

                        if (firstType == false) {
                            _writer.WriteLine(",");
                        }
                        TypeGenerator.GenerateRegistrationScript(this, type);
                        firstType = false;
                    }
                }
                _writer.Indent--;
                _writer.WriteLine();
                _writer.Write("}");
                _writer.Indent--;
            }
            else {
                _writer.Write(" null");
            }
            _writer.WriteLine(");");
            _writer.WriteLine();

            foreach (TypeSymbol type in types) {
                if (type.Type == SymbolType.Class) {
                    TypeGenerator.GenerateClassConstructorScript(this, (ClassSymbol)type);
                }
            }

            if (_options.IncludeTests) {
                foreach (TypeSymbol type in types) {
                    ClassSymbol classSymbol = type as ClassSymbol;
                    if ((classSymbol != null) && classSymbol.IsTestClass) {
                        TestGenerator.GenerateScript(this, classSymbol);
                    }
                }
            }

            _writer.WriteLine();
            _writer.WriteLine("return $" +  moduleName + ";");

            _writer.Indent--;
            _writer.WriteLine("});");
        }


        private sealed class TypeComparer : IComparer<TypeSymbol> {

            public int Compare(TypeSymbol x, TypeSymbol y) {
                if (x.Type != y.Type) {
                    // If types are different, then use the symbol type to
                    // similar types of types together.
                    return (int)x.Type - (int)y.Type;
                }

                if (x.Type == SymbolType.Class) {
                    // For classes, sort by inheritance depth. This is a crude
                    // way to ensure the base class for a class is generated
                    // first, without specifically looking at the inheritance
                    // chain for any particular type. A parent class with lesser
                    // inheritance depth has to by definition come first.

                    return ((ClassSymbol)x).InheritanceDepth - ((ClassSymbol)y).InheritanceDepth;
                }

                return 0;
            }
        }
    }
}
