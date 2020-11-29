using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GMath.CodeGeneration
{
    class Program
    {
        static string NameSpace = "GMath";
        enum VectorType
        {
            Float,
            Integer
        }

        #region Type Generation

        static string[] VectorComponents = { "x", "y", "z", "w" };

        static string TypeName(VectorType type)
        {
            switch (type)
            {
                case VectorType.Float: return "float";
                case VectorType.Integer: return "int";
                default: throw new NotSupportedException();
            }
        }

        static bool CanCastImplicit (VectorType f, VectorType t)
        {
            if (t == VectorType.Integer)
                return true;

            return false;
        }

        static string CodeForVectorType(VectorType type, int components)
        {
            StringBuilder code = new StringBuilder();
            string typeName = TypeName(type);

            code.AppendLine("namespace " + NameSpace + " {");

            // Struct definition: struct float3 {
            code.AppendFormat("public struct {0}{1}{{\n", typeName, components);

            // Components definitions: public float x;
            for (int i = 0; i < components; i++)
                code.AppendFormat("\tpublic {0} {1};\n", typeName, VectorComponents[i]);

            // Accessors
            for (int i = 2; i < components; i++)
                code.AppendFormat("\tpublic {0}{1} {2} {{ get {{ return new {0}{1}({3}); }} }}\n",
                    typeName,
                    i,
                    string.Join("", Enumerable.Range(0, i).Select(c => VectorComponents[c])),
                    string.Join(", ", Enumerable.Range(0, i).Select(c => "this." + VectorComponents[c])));

            // Indexer
            code.AppendFormat("\tpublic {0} this[int idx] {{\n",
                typeName);
            // gets
            code.AppendLine("\t\tget{");
            for (int i = 0; i < components; i++)
                code.AppendFormat("\t\t\tif(idx == {0}) return this.{1};\n", i, VectorComponents[i]);
            code.AppendLine("\t\t\treturn 0; // Silent return ... valid for HLSL");
            code.AppendLine("\t\t}");
            // sets
            code.AppendLine("\t\tset{");
            for (int i = 0; i < components; i++)
                code.AppendFormat("\t\t\tif(idx == {0}) this.{1} = value;\n", i, VectorComponents[i]);
            code.AppendLine("\t\t}");
            // indexer end
            code.AppendLine("\t}");

            // Full constructor: public float3(float x, float y, float z) { this.x = x; ... }
            code.AppendFormat("\tpublic {0}{1}({2}){{\n",
                typeName,
                components,
                string.Join(',', Enumerable.Range(0, components).Select(c => string.Format("{0} {1}", typeName, VectorComponents[c]))));
            // Assigns
            for (int i = 0; i < components; i++)
                code.AppendFormat("\t\tthis.{0}={0};\n", VectorComponents[i]);
            // Full constructor end
            code.AppendLine("\t}");

            if (components > 1)
                // Promotion constructor: public float3(float v):this(v,v,v){}
                code.AppendFormat("\tpublic {0}{1}({0} v):this({2}){{}}\n",
                    typeName,
                    components,
                    string.Join(',', Enumerable.Range(0, components).Select(c => "v")));

            // Explicit conversions (Demotions)
            for (int d = 1; d < components; d++)
            {
                // public static explicit operator float2 (float3 v) { return new float2(v.x, v.y); }
                code.AppendFormat("\tpublic static explicit operator {0}{1}({0}{2} v) {{ return new {0}{1}({3}); }}\n",
                    typeName, d, components, string.Join(", ", Enumerable.Range(0, d).Select(c => string.Format("v.{0}", VectorComponents[c]))));
            }
            // Implicit promotion
            code.AppendFormat("\tpublic static implicit operator {0}{1}({0} v) {{ return new {0}{1}(v); }}\n", typeName, components);

            foreach (VectorType otherType in Enum.GetValues(typeof(VectorType)))
                if (otherType != type)
                {
                    string otherTypeName = TypeName(otherType);
                    // Explicit conversion to parallel type
                    code.AppendFormat("\tpublic static {4} operator {0}{1}({2}{1} v) {{ return new {0}{1}({3}); }}\n",
                        otherTypeName,
                        components,
                        typeName,
                        string.Join(',', Enumerable.Range(0, components).Select(c => string.Format("({0})v.{1}", otherTypeName, VectorComponents[c]))),
                        CanCastImplicit(type, otherType) ? "implicit":"explicit");
                }

            var unaryOperators = Enumerable.Empty<string>();

            unaryOperators = unaryOperators.Union(new string[] { "-", "+" }); // aritmetic

            if (type == VectorType.Integer)
                unaryOperators = unaryOperators.Union(new string[] { "~" }); // logic

            // Binary component wise aritmetic operators
            foreach (var op in unaryOperators)
            {
                code.AppendFormat("\tpublic static {0}{1} operator {2}({0}{1} a) {{ return new {0}{1}({3}); }}\n",
                    typeName, components,
                    op,
                    string.Join(',', Enumerable.Range(0, components).Select(c => string.Format("{1}a.{0}", VectorComponents[c], op))));
            }

            code.AppendFormat("\tpublic static int{1} operator {2}({0}{1} a) {{ return new int{1}({3}); }}\n",
                    typeName, components,
                    "!",
                    string.Join(',', Enumerable.Range(0, components).Select(c => string.Format("a.{0}==0?1:0", VectorComponents[c]))));

            var binaryOperators = Enumerable.Empty<string>();

            binaryOperators = binaryOperators.Union(new string[] { "+", "*", "-", "/", "%" }); // aritmetic

            if (type == VectorType.Integer)
                binaryOperators = binaryOperators.Union(new string[] { "&", "|", "^" }); // logic

            // Binary component wise aritmetic operators
            foreach (var op in binaryOperators)
            {
                code.AppendFormat("\tpublic static {0}{1} operator {2}({0}{1} a, {0}{1} b) {{ return new {0}{1}({3}); }}\n",
                    typeName, components,
                    op,
                    string.Join(',', Enumerable.Range(0, components).Select(c => string.Format("a.{0} {1} b.{0}", VectorComponents[c], op))));
            }

            // Comparison component wise aritmetic operators
            foreach (var op in new string[] { "==", "!=", "<", "<=", ">=", ">" })
            {
                code.AppendFormat("\tpublic static int{1} operator {2}({0}{1} a, {0}{1} b) {{ return new int{1}({3}); }}\n",
                    typeName, components,
                    op,
                    string.Join(", ", Enumerable.Range(0, components).Select(c => string.Format("(a.{0} {1} b.{0})?1:0", VectorComponents[c], op))));
            }

            // ToString
            code.AppendFormat("\tpublic override string ToString() {{ return string.Format(\"({0})\", {1}); }}\n",
                string.Join(", ", Enumerable.Range(0, components).Select(c => "{" + c + "}")),
                string.Join(", ", Enumerable.Range(0, components).Select(c => "this." + VectorComponents[c])));

            // Struct end
            code.AppendLine("}");

            // namespace end
            code.AppendLine("}");

            return code.ToString();
        }

        static string[,] MatrixComponents = {
        { "_m00", "_m01", "_m02", "_m03" },
        { "_m10", "_m11", "_m12", "_m13" },
        { "_m20", "_m21", "_m22", "_m23" },
        { "_m30", "_m31", "_m32", "_m33" }
        };

        static string CodeForMatrixType(VectorType type, int rows, int cols)
        {
            StringBuilder code = new StringBuilder();
            string typeName = TypeName(type);

            code.AppendLine("namespace " + NameSpace + " {");

            // Struct definition: struct float3x4 {
            code.AppendFormat("public struct {0}{1}x{2}{{\n", typeName, rows, cols);

            // Components definitions: public float _m02;
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    code.AppendFormat("\tpublic {0} {1};\n", typeName, MatrixComponents[i, j]);

            // Indexer
            code.AppendFormat("\tpublic {0}{1} this[int row] {{\n",
                typeName, cols);
            // gets
            code.AppendLine("\t\tget{");
            for (int i = 0; i < rows; i++)
                code.AppendFormat("\t\t\tif(row == {0}) return new {1}{2} ({3});\n", i, typeName, cols, string.Join(", ", Enumerable.Range(0, cols).Select(c => MatrixComponents[i, c])));
            code.AppendLine("\t\t\treturn 0; // Silent return ... valid for HLSL");
            code.AppendLine("\t\t}");
            // indexer end
            code.AppendLine("\t}");

            // Full constructor: public float3x4(float _m00, float _m01, float _m02, ... ) { this._m00 = _m00; ... }
            code.AppendFormat("\tpublic {0}{1}x{2}({3}){{\n",
                typeName,
                rows, cols,
                string.Join(',', Enumerable.Range(0, rows * cols).Select(i => string.Format("{0} {1}", typeName, MatrixComponents[i / cols, i % cols]))));
            // Assigns
            for (int i = 0; i < rows * cols; i++)
                code.AppendFormat("\t\tthis.{0}={0};\n", MatrixComponents[i / cols, i % cols]);
            // Full constructor end
            code.AppendLine("\t}");

            if (rows > 1 || cols > 1)
                // Promotion constructor: public float3(float v):this(v,v,v){}
                code.AppendFormat("\tpublic {0}{1}x{2}({0} v):this({3}){{}}\n",
                    typeName,
                    rows, cols,
                    string.Join(',', Enumerable.Range(0, rows*cols).Select(c => "v")));

            if (rows == 1) // Can convert to and from a vector
            {
                code.AppendFormat("\tpublic static implicit operator {0}{1}({0}1x{1} m) {{ return new {0}{1}({2}); }}\n",
                                            typeName, cols, string.Join(", ", Enumerable.Range(0, cols).Select(k => string.Format("m.{0}", MatrixComponents[0, k]))));
                code.AppendFormat("\tpublic static implicit operator {0}1x{1}({0}{1} v) {{ return new {0}1x{1}({2}); }}\n",
                                            typeName, cols, string.Join(", ", Enumerable.Range(0, cols).Select(k => string.Format("v.{0}", VectorComponents[k]))));
            }
            // Explicit conversions (Demotions)
            for (int r = 1; r <= rows; r++)
                for (int c = 1; c <= cols; c++)
                    if (r < rows || c < cols)
                    {
                        code.AppendFormat("\tpublic static explicit operator {0}{1}x{2}({0}{3}x{4} m) {{ return new {0}{1}x{2}({5}); }}\n",
                            typeName, r, c, rows, cols, string.Join(", ", Enumerable.Range(0, r * c).Select(k => string.Format("m.{0}", MatrixComponents[k / c, k % c]))));
                    }
            // Implicit promotion
            //if (rows > 1 || cols > 1)
            code.AppendFormat("\tpublic static implicit operator {0}{1}x{2}({0} v) {{ return new {0}{1}x{2}(v); }}\n", typeName, rows, cols);

            foreach (VectorType otherType in Enum.GetValues(typeof(VectorType)))
                if (otherType != type)
                {
                    string otherTypeName = TypeName(otherType);
                    // Explicit conversion to parallel type
                    code.AppendFormat("\tpublic static {5} operator {0}{1}x{2}({3}{1}x{2} v) {{ return new {0}{1}x{2}({4}); }}\n",
                        otherTypeName,
                        rows, cols,
                        typeName,
                        string.Join(',', Enumerable.Range(0, rows*cols).Select(c => string.Format("({0})v.{1}", otherTypeName, MatrixComponents[c/cols, c%cols]))),
                        CanCastImplicit(type, otherType) ? "implicit" : "explicit");
                }

            var unaryOperators = Enumerable.Empty<string>();

            unaryOperators = unaryOperators.Union(new string[] { "-", "+" }); // aritmetic

            if (type == VectorType.Integer)
                unaryOperators = unaryOperators.Union(new string[] { "~" }); // logic

            // Binary component wise aritmetic operators
            foreach (var op in unaryOperators)
            {
                code.AppendFormat("\tpublic static {0}{1}x{2} operator {3}({0}{1}x{2} a) {{ return new {0}{1}x{2}({4}); }}\n",
                    typeName, rows, cols,
                    op,
                    string.Join(',', Enumerable.Range(0, rows * cols).Select(c => string.Format("{1}a.{0}", MatrixComponents[c / cols, c % cols], op))));
            }

            code.AppendFormat("\tpublic static int{1}x{2} operator {3}({0}{1}x{2} a) {{ return new int{1}x{2}({4}); }}\n",
                    typeName, rows, cols,
                    "!",
                    string.Join(',', Enumerable.Range(0, rows * cols).Select(c => string.Format("a.{0}==0?1:0", MatrixComponents[c / cols, c % cols]))));

            var binaryOperators = Enumerable.Empty<string>();

            binaryOperators = binaryOperators.Union(new string[] { "+", "*", "-", "/", "%" }); // aritmetic

            if (type == VectorType.Integer)
                binaryOperators = binaryOperators.Union(new string[] { "&", "|", "^" }); // logic

            // Binary component wise aritmetic operators
            foreach (var op in binaryOperators)
            {
                code.AppendFormat("\tpublic static {0}{1}x{2} operator {3}({0}{1}x{2} a, {0}{1}x{2} b) {{ return new {0}{1}x{2}({4}); }}\n",
                    typeName, rows, cols,
                    op,
                    string.Join(',', Enumerable.Range(0, rows * cols).Select(c => string.Format("a.{0} {1} b.{0}", MatrixComponents[c/cols,c%cols], op))));
            }

            // Comparison component wise aritmetic operators
            foreach (var op in new string[] { "==", "!=", "<", "<=", ">=", ">" })
            {
                code.AppendFormat("\tpublic static int{1}x{2} operator {3}({0}{1}x{2} a, {0}{1}x{2} b) {{ return new int{1}x{2}({4}); }}\n",
                    typeName, rows, cols,
                    op,
                    string.Join(", ", Enumerable.Range(0, rows*cols).Select(c => string.Format("(a.{0} {1} b.{0})?1:0", MatrixComponents[c/cols, c%cols], op))));
            }

            // ToString
            code.AppendFormat("\tpublic override string ToString() {{ return string.Format(\"({0})\", {1}); }}\n",
                string.Join(", ", Enumerable.Range(0, rows).Select(r => "(" + string.Join(", ", Enumerable.Range(r * cols, cols).Select(c => "{" + c + "}"))+")")),
                string.Join(", ", Enumerable.Range(0, rows * cols).Select(c => "this." + MatrixComponents[c / cols, c % cols])));

            // Struct end
            code.AppendLine("}");

            // namespace end
            code.AppendLine("}");

            return code.ToString();
        }

        #endregion

        #region Functions Generation

        static string CodeForFunction(string functionName, string type, int args, string returnExp, bool statement = false)
        {
            string[] parameters = new string[] { };
            switch (args)
            {
                case 1: parameters = new string[] { "v" }; break;
                case 2: parameters = new string[] { "a", "b" }; break;
                case 3: parameters = new string[] { "a", "b", "c" }; break;
            }
            string parametersDec = string.Join(", ", parameters.Select(p => type + " " + p));
            return string.Format(
                statement ?
                "\tpublic static {0} {1}({2}) {{ {3} }}" :
                "\tpublic static {0} {1}({2}) {{ return {3}; }}",
                type,
                functionName,
                parametersDec,
                string.Format(returnExp, (object[])parameters)
                );
        }

        static string CodeForFunctionInAllTypes(string functionName, string returnExp, int args = 1, bool statement = false)
        {
            StringBuilder code = new StringBuilder();

            code.AppendLine("\t#region " + functionName);

            code.AppendLine(CodeForFunction(functionName, "float", args, returnExp, statement));

            for (int c = 1; c <= 4; c++)
                code.AppendLine(CodeForFunction(functionName, "float" + c, args, returnExp, statement));
          
            for (int r = 1; r <= 4; r++)
                for (int c = 1; c <= 4; c++)
                    code.AppendLine(CodeForFunction(functionName, "float" + r + "x" + c, args, returnExp, statement));

            code.AppendLine("\t#endregion");
            code.AppendLine();

            return code.ToString();
        }

        static string CodeForFunctionInAllVectors(string functionName, string returnExp, int args = 1)
        {
            StringBuilder code = new StringBuilder();

            code.AppendLine("\t#region " + functionName);

            for (int c = 1; c <= 4; c++)
                code.AppendLine(CodeForFunction(functionName, "float" + c, args, returnExp));

            code.AppendLine("\t#endregion");
            code.AppendLine();

            return code.ToString();
        }

        static string CodeForComponentwiseFunction(string functionName, string type, int args, int totalComponents, Func<string[], int, string> perComponentExp)
        {
            string[] parameters = new string[] { };
            switch (args)
            {
                case 1: parameters = new string[] { "v" }; break;
                case 2: parameters = new string[] { "a", "b" }; break;
                case 3: parameters = new string[] { "a", "b", "c" }; break;
            }
            string parametersDec = string.Join(", ", parameters.Select(p => type + " " + p));
            return string.Format("\tpublic static {0} {1}({2}) {{ return new {0}({3}); }}",
                type,
                functionName,
                parametersDec,
                string.Join(", ", Enumerable.Range(0, totalComponents).Select(c => perComponentExp(parameters, c)))
                );
        }
        static string CodeForFunctionInFloat(string functionName, string returnExp, int args)
        {
            StringBuilder code = new StringBuilder();

            code.AppendLine(CodeForFunction(functionName, "float", args, returnExp));
           
            return code.ToString();
        }
        static string CodeForComponentwiseFunctionInVectors(string functionName, string perComponentExp, int args)
        {
            StringBuilder code = new StringBuilder();

            for (int c = 1; c <= 4; c++)
                code.AppendLine(CodeForComponentwiseFunction(functionName, "float" + c, args, c, (p, i) => string.Format(perComponentExp, (object[])p.Select(par=>string.Format("{0}.{1}", par, VectorComponents[i])).ToArray())));

            return code.ToString();
        }
        static string CodeForComponentwiseFunctionInMatrices(string functionName, string perComponentExp, int args)
        {
            StringBuilder code = new StringBuilder();

            for (int r = 1; r <= 4; r++)
                for (int c = 1; c <= 4; c++)
                    code.AppendLine(CodeForComponentwiseFunction(functionName, "float" + r + "x" + c, args, r * c,
                        (p, i) => string.Format(perComponentExp, (object[])p.Select(par => string.Format("{0}.{1}", par, MatrixComponents[i / c, i % c])).ToArray())));

            return code.ToString();
        }

        static string CodeForConstructor(string typename, IEnumerable<string> parameters)
        {
            string args = string.Join(", ", parameters.Select(p => "float " + p));
            return string.Format("\tpublic static {0} {0}({1}) {{ return new {0}({2}); }}\n",
                typename,
                args,
                string.Join(", ", parameters)
                );
        }

        static string CodeForConstructorFunctionInAllTypes()
        {
            StringBuilder code = new StringBuilder();
            code.AppendLine("\t#region Constructors");

            for (int c = 1; c <= 4; c++)
                code.AppendLine(CodeForConstructor("float" + c, VectorComponents.Take(c)));

            for (int r = 1; r <= 4; r++)
                for (int c = 1; c <= 4; c++)
                {
                    List<string> submatrixArgs = new List<string>();
                    for (int i = 0; i < r; i++)
                        for (int j = 0; j < c; j++)
                            submatrixArgs.Add(MatrixComponents[i, j]);
                    code.AppendLine(CodeForConstructor("float" + r + "x" + c, submatrixArgs));
                }
            code.AppendLine("\t#endregion");
            code.AppendLine();
            return code.ToString();
        }

        static string CodeForDot(string functionName)
        {
            StringBuilder code = new StringBuilder();

            code.AppendLine("\t#region " + functionName);

            for (int c = 1; c <= 4; c++)
            {
                code.AppendFormat("\tpublic static float {2}(float{0} a, float{0} b) {{ return {1}; }}\n", c,
                    string.Join(" + ", Enumerable.Range(0, c).Select(i => string.Format("a.{0} * b.{0}", VectorComponents[i]))),
                    functionName);
            }
            code.AppendLine("\t#endregion");
            code.AppendLine();
            return code.ToString();
        }

        static string CodeForAny(string functionName)
        {
            StringBuilder code = new StringBuilder();

            code.AppendLine("\t#region " + functionName);

            for (int c = 1; c <= 4; c++)
            {
                code.AppendFormat("\tpublic static bool {2}(float{0} v) {{ return {1}; }}\n", c,
                    string.Join(" || ", Enumerable.Range(0, c).Select(i => string.Format("(v.{0} != 0)", VectorComponents[i]))),
                    functionName);
            }

            for (int r = 1; r <= 4; r++)
                for (int c = 1; c <= 4; c++)
                {
                    code.AppendFormat("\tpublic static bool {3}(float{0}x{1} m) {{ return {2}; }}\n", r, c,
                        string.Join(" || ", Enumerable.Range(0, r * c).Select(i => string.Format("(m.{0} != 0)", MatrixComponents[i / c, i % c]))),
                        functionName);
                }

            code.AppendLine("\t#endregion");
            code.AppendLine();

            return code.ToString();
        }

        static string CodeForAll(string functionName)
        {
            StringBuilder code = new StringBuilder();

            code.AppendLine("\t#region " + functionName);

            for (int c = 1; c <= 4; c++)
            {
                code.AppendFormat("\tpublic static bool {2}(float{0} v) {{ return {1}; }}\n", c,
                    string.Join(" && ", Enumerable.Range(0, c).Select(i => string.Format("(v.{0} != 0)", VectorComponents[i]))),
                    functionName);
            }

            for (int r = 1; r <= 4; r++)
                for (int c = 1; c <= 4; c++)
                {
                    code.AppendFormat("\tpublic static bool {3}(float{0}x{1} m) {{ return {2}; }}\n", r, c,
                        string.Join(" && ", Enumerable.Range(0, r * c).Select(i => string.Format("(m.{0} != 0)", MatrixComponents[i / c, i % c]))),
                        functionName);
                }
            code.AppendLine("\t#endregion");
            code.AppendLine();
            return code.ToString();
        }

        static string CodeForAbsDot(string functionName)
        {
            StringBuilder code = new StringBuilder();

            code.AppendLine("\t#region " + functionName);

            for (int c = 1; c <= 4; c++)
            {
                code.AppendFormat("\tpublic static float {2}(float{0} a, float{0} b) {{ return abs({1}); }}\n", c,
                    string.Join(" + ", Enumerable.Range(0, c).Select(i => string.Format("a.{0} * b.{0}", VectorComponents[i]))),
                    functionName);
            }
            code.AppendLine("\t#endregion");
            code.AppendLine();
            return code.ToString();
        }

        static string CodeForMul(string functionName)
        {
            StringBuilder code = new StringBuilder();
            code.AppendLine("\t#region " + functionName);

            for (int r = 1; r <= 4; r++)
                for(int k = 1; k <= 4; k++)
                    for(int c = 1; c <= 4; c++)
                    {
                        // generating the multiplication code for the mul
                        // rxk * kxc = rxc

                        code.AppendFormat("\tpublic static float{0}x{2} {4}(float{0}x{1} a, float{1}x{2} b) {{ return new float{0}x{2}({3}); }}\n",
                            r, k, c, 
                            string.Join(", ", Enumerable.Range(0, r*c).Select(
                                i =>
                                {
                                    int fr = i / c;
                                    int fc = i % c;
                                    return string.Join("+", Enumerable.Range(0, k).Select(o => string.Format("a.{0}*b.{1}", MatrixComponents[fr, o], MatrixComponents[o, fc])));
                                }
                                )), functionName);
                    }

            code.AppendLine("\t#endregion");
            code.AppendLine();

            return code.ToString();
        }

        static string CodeForTranspose(string functionName)
        {
            StringBuilder code = new StringBuilder();
            code.AppendLine("\t#region " + functionName);

            for (int r = 1; r <= 4; r++)
                for (int c = 1; c <= 4; c++)
                {
                    // generating the multiplication code for the mul
                    // rxk * kxc = rxc

                    code.AppendFormat("\tpublic static float{1}x{0} {3}(float{0}x{1} a) {{ return new float{1}x{0}({2}); }}\n",
                        c, r,
                        string.Join(", ", Enumerable.Range(0, r * c).Select(
                            i =>
                            {
                                int fr = i / c;
                                int fc = i % c;
                                return string.Format("a.{0}", MatrixComponents[fc, fr]);
                            }
                            )), functionName);
                }

            code.AppendLine("\t#endregion");
            code.AppendLine();

            return code.ToString();
        }


        static string CodeForComponentwiseFunction(string functionName, string perComponentExp, int args = 1)
        {
            StringBuilder code = new StringBuilder();

            code.AppendLine("\t#region " + functionName);

            code.AppendLine(CodeForFunctionInFloat(functionName, perComponentExp, args));
            code.AppendLine(CodeForComponentwiseFunctionInVectors(functionName, perComponentExp, args));
            code.AppendLine(CodeForComponentwiseFunctionInMatrices(functionName, perComponentExp, args));

            code.AppendLine("\t#endregion");
            code.AppendLine();

            return code.ToString();
        }


        static string CodeForFunctions()
        {
            StringBuilder code = new StringBuilder();
            
            code.AppendLine("using System;");

            code.AppendLine("namespace " + NameSpace + " {");

            code.AppendLine("public static partial class Gfx {");

            code.AppendLine(CodeForComponentwiseFunction("abs", "(float)Math.Abs({0})"));
            code.AppendLine(CodeForAbsDot("absdot"));
            code.AppendLine(CodeForComponentwiseFunction("acos", "(float)Math.Acos({0})"));
            code.AppendLine(CodeForAll("all"));
            code.AppendLine(CodeForAny("any"));
            code.AppendLine(CodeForComponentwiseFunction("asin", "(float)Math.Asin({0})"));
            code.AppendLine(CodeForComponentwiseFunction("atan", "(float)Math.Atan({0})"));
            code.AppendLine(CodeForComponentwiseFunction("atan2", "(float)Math.Atan2({0}, {1})", 2));
            code.AppendLine(CodeForComponentwiseFunction("ceil", "(float)Math.Ceiling({0})"));
            code.AppendLine(CodeForFunctionInAllTypes("clamp", "max({1}, min({2}, {0}))", 3));
            code.AppendLine(CodeForComponentwiseFunction("cos", "(float)Math.Cos({0})"));
            code.AppendLine(CodeForComponentwiseFunction("cosh", "(float)Math.Cosh({0})"));
            code.AppendLine(CodeForDot("dot"));
            code.AppendLine(CodeForComponentwiseFunction("min", "{0}<{1}?{0}:{1}", 2));
            code.AppendLine(CodeForComponentwiseFunction("max", "{0}>{1}?{0}:{1}", 2));
            code.AppendLine(CodeForComponentwiseFunction("degrees", "(float)({0}*180.0/Math.PI)", 1));
            code.AppendLine(CodeForFunctionInAllVectors("length", "(float)Math.Sqrt(dot({0}, {0}))", 1));
            code.AppendLine(CodeForFunctionInAllVectors("sqrlength", "dot({0}, {0})", 1));
            code.AppendLine(CodeForFunctionInAllVectors("distance", "length({0} - {1})", 2));
            code.AppendLine(CodeForFunctionInAllVectors("sqrdistance", "sqrlength({0} - {1})", 2));
            code.AppendLine(CodeForComponentwiseFunction("exp", "(float)Math.Exp({0})"));
            code.AppendLine(CodeForComponentwiseFunction("exp2", "(float)Math.Pow(2, {0})"));
            code.AppendLine(CodeForComponentwiseFunction("floor", "(float)Math.Floor({0})"));
            code.AppendLine(CodeForFunctionInAllTypes("fmod", "{0} % {1}", 2));
            code.AppendLine(CodeForFunctionInAllTypes("frac", "{0} % 1", 1));
            code.AppendLine(CodeForFunctionInAllTypes("ldexp", "{0} * exp2({1})", 2));
            code.AppendLine(CodeForFunctionInAllTypes("lerp", "{0} + {2}*({1} - {0})", 3));
            code.AppendLine(CodeForComponentwiseFunction("log", "(float)Math.Log({0})"));
            code.AppendLine(CodeForComponentwiseFunction("log10", "(float)Math.Log10({0})"));
            code.AppendLine(CodeForComponentwiseFunction("log2", "(float)Math.Log({0}, 2)"));
            code.AppendLine(CodeForMul("mul"));
            code.AppendLine(CodeForFunctionInAllVectors("normalize", "any({0})?{0}/length({0}) : 0", 1));
            code.AppendLine(CodeForComponentwiseFunction("pow", "(float)Math.Pow({0},{1})", 2));
            code.AppendLine(CodeForComponentwiseFunction("radians", "(float)({0}*Math.PI/180)", 1));
            code.AppendLine(CodeForComponentwiseFunction("round", "(float)Math.Round({0})"));
            code.AppendLine(CodeForComponentwiseFunction("rsqrt", "(float)(1.0/Math.Sqrt({0}))"));
            code.AppendLine(CodeForFunctionInAllTypes("saturate", "max(0, min(1, {0}))", 1));
            code.AppendLine(CodeForComponentwiseFunction("sign", "(float)Math.Sign({0})"));
            code.AppendLine(CodeForComponentwiseFunction("sin", "(float)Math.Sin({0})"));
            code.AppendLine(CodeForComponentwiseFunction("sinh", "(float)Math.Sinh({0})"));
            code.AppendLine(CodeForFunctionInAllTypes("smoothstep", "var t = saturate(({2} - {0})/({1} - {0})); return t*t*(3 - 2 * t); ", 3, true));
            code.AppendLine(CodeForComponentwiseFunction("sqrt", "(float)Math.Sqrt({0})"));
            code.AppendLine(CodeForComponentwiseFunction("step", "{0} >= {1} ? 1 : 0", 2));
            code.AppendLine(CodeForComponentwiseFunction("tan", "(float)Math.Tan({0})"));
            code.AppendLine(CodeForComponentwiseFunction("tanh", "(float)Math.Tanh({0})"));
            code.AppendLine(CodeForTranspose("transpose"));

            code.AppendLine(CodeForConstructorFunctionInAllTypes());

            code.AppendLine("}");

            code.AppendLine("}");

            return code.ToString();
        }

        #endregion

        static void Main(string[] args)
        {
            foreach (var type in new VectorType[] { VectorType.Float, VectorType.Integer })
            {
                for (int c = 1; c <= 4; c++)
                    File.WriteAllText(TypeName(type) + "" + c + ".cs", CodeForVectorType(type, c));

                for (int r = 1; r <= 4; r++)
                    for (int c = 1; c <= 4; c++)
                        File.WriteAllText(TypeName(type) + "" + r + "x" + c + ".cs", CodeForMatrixType(type, r, c));
            }

            File.WriteAllText("Functions.cs", CodeForFunctions());
        }
    }
}
