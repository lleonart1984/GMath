using System;
using static GMath.Gfx;
using static GMath.GTools;
using static GMath.GRandom;

namespace GMath.Tests
{
    class Program
    {
        static float3 generateInTriangle(float3 t1, float3 t2, float3 t3)
        {
            float3 b = random3();
            b /= (b.x + b.y + b.z);
            return t1 * b.x + t2 * b.y + t3 * b.z;
        }

        static float3 generateInTriangle(float3 t1, float3 t2, float3 t3, float3 closeTo)
        {
            float3 b = barycenter(closeTo, t1, t2, t3);
            float3 mut = gauss3(b, 0.01f);
            mut = saturate(mut);
            mut /= max(0.0000001f, mut.x + mut.y + mut.z);
            mut.z = 1 - mut.x - mut.y;
            return t1 * mut.x + t2 * mut.y + t3 * mut.z;
        }

        static float3 generateInSegment(float3 a, float3 b)
        {
            float alpha = random();
            return a * (1 - alpha) + b * alpha;
        }

        static float3 generateInSegment(float3 a, float3 b, float3 closeTo)
        {
            float2 bar = barycenter(closeTo, a, b);
            float2 mut = gauss2(bar, 0.01f);
            mut = saturate(mut);
            mut.y = 1 - mut.x;
            return mut.x * a + mut.y * b;
        }

        static void Test_ClosestPtoInSeg(float3 p, float3 a, float3 b)
        {
            float closestDistance = distanceP2S(p, a, b);

            float clst = distanceP2P(p, a);
            float3 sampleInSegment = a;

            while (true)
            {
                sampleInSegment = generateInSegment(a, b, sampleInSegment);
                if (clst > distance(sampleInSegment, p))
                {
                    clst = distance(sampleInSegment, p);
                    Console.WriteLine("{0} <- {1}", closestDistance, clst);
                }
            }
        }

        static void Test_ClosestPtoInTriangle(float3 p, float3 a, float3 b, float3 c) {
            float closestDistance = distanceP2T(p, a, b, c);

            float clst = distanceP2P(p, a);
            float3 sampleInTriangle = a;

            while (true)
            {
                sampleInTriangle = generateInTriangle(a, b, c, sampleInTriangle);
                if (clst > distance(sampleInTriangle, p))
                {
                    clst = distance(sampleInTriangle, p);
                    Console.WriteLine("{0} <- {1}", closestDistance, clst);
                }
            }
        }

        static void Test_ClosestSegInSeg(float3 a1, float3 b1, float3 a2, float3 b2)
        {
            float closestDistance = distanceS2S(a1, b1, a2, b2);

            float3 sampleInSegment1 = a1;
            float3 sampleInSegment2 = a2;
            float clst = distanceP2P(sampleInSegment1, sampleInSegment2);

            while (true)
            {
                sampleInSegment1 = generateInSegment(a1, b1, sampleInSegment1);
                sampleInSegment2 = generateInSegment(a2, b2, sampleInSegment2);

                if (clst > distance(sampleInSegment1, sampleInSegment2))
                {
                    clst = distance(sampleInSegment1, sampleInSegment2);
                    Console.WriteLine("{0} <- {1}", closestDistance, clst);
                }
            }
        }

        static void Test_ClosestSegInTriangle(float3 a, float3 b, float3 t1, float3 t2, float3 t3)
        {
            float closestDistance = distanceS2T(a, b, t1, t2, t3);

            float3 sampleInSegment = a;
            float3 sampleInTriangle = t1;
            float clst = distanceP2P(sampleInSegment, sampleInTriangle);

            while (true)
            {
                sampleInSegment = generateInSegment(a, b, sampleInSegment);
                sampleInTriangle = generateInTriangle(t1, t2, t3, sampleInTriangle);

                if (clst > distance(sampleInSegment, sampleInTriangle))
                {
                    clst = distance(sampleInSegment, sampleInTriangle);
                    Console.WriteLine("{0} <- {1}", closestDistance, clst);
                }
            }
        }

        static void Test_ClosestQuadInTriangle(float3 C, float3 U, float3 R, float3 N, float3 t1, float3 t2, float3 t3)
        {
            float closestDistance = distanceQ2T(C, U, R, N, t1, t2, t3);

            float3 sampleInQuad = C;
            float3 sampleInTriangle = t1;
            float clst = distanceP2P(sampleInQuad, sampleInTriangle);
            float2 lastRnd = float2(0, 0);
            while (true)
            {
                lastRnd = gauss2(lastRnd, 0.1f);
                lastRnd = saturate(lastRnd);
                sampleInQuad = C + U * lastRnd.x + R * lastRnd.y;

                sampleInTriangle = generateInTriangle(t1, t2, t3, sampleInTriangle);

                if (clst > distance(sampleInQuad, sampleInTriangle))
                {
                    clst = distance(sampleInQuad, sampleInTriangle);
                    Console.WriteLine("{0} <- {1}", closestDistance, clst);
                }
            }
        }

        static void Main(string[] args)
        {
            //Test_ClosestPtoInSeg(float3(3, 1, 1) * 20, float3(-1, 3, 2) * 20, float3(2, 2, 1.5f) * 20);
            //Test_ClosestPtoInTriangle(float3(1,1,0), float3(2, 1, 3), float3(3, -1, -2), float3(1.5f, 2, 4));
            //Test_ClosestPtoInTriangle(float3(1,1,1), float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1));
            //Test_ClosestSegInTriangle(float3(1,2,1), float3(3,2,0), float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1));
            //Test_ClosestSegInSeg(float3(1, 0, 0), float3(1, 1, 0), float3(2, 1, 3), float3(3, -1, -2));
            //Test_ClosestSegInTriangle(float3(1, 0, 0), float3(1, 1, 0), float3(2, 1, 3), float3(3, -1, -2), float3(1.5f, 2, 4));
            Test_ClosestQuadInTriangle(float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1), float3(1, 0, 0), float3(2, 1, 3), float3(3, -1, -2), float3(1.5f, 2, 4));
        }

    }
}
