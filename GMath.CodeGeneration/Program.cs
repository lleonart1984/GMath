using System;
using System.IO;
using System.Linq;
using System.Text;

namespace GMath.CodeGeneration
{
    class Program
    {

        enum VectorType
        {
            Float,
            Integer
        }
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

        static string NameSpace = "GMath";

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
                         "explicit");
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
                         "explicit");
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
        }
    }
}
