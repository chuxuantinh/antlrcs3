/*
 * [The "BSD licence"]
 * Copyright (c) 2005-2008 Terence Parr
 * All rights reserved.
 *
 * Conversion to C#:
 * Copyright (c) 2008 Sam Harwell, Pixel Mine, Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

namespace Antlr3.Misc
{
    using Math = System.Math;
    using ArgumentException = System.ArgumentException;

    /** An immutable inclusive interval a..b */
    public struct Interval
    {
        public readonly int a;
        public readonly int b;

        private Interval( int a, int b )
        {
            if (a > b)
                throw new ArgumentException();

            this.a = a;
            this.b = b;
        }

        public int Length
        {
            get
            {
                return b - a + 1;
            }
        }

        public static Interval FromBounds( int start, int endInclusive )
        {
            return new Interval( start, endInclusive );
        }

        public override int GetHashCode()
        {
            return a + b;
        }

        public bool Equals(Interval other)
        {
            return this.a == other.a && this.b == other.b;
        }

        public override bool Equals( object o )
        {
            if (!(o is Interval))
                return false;

            return this.Equals((Interval)o);
        }

        /** Does this start completely before other? Disjoint */
        public bool StartsBeforeDisjoint( Interval other )
        {
            return this.a < other.a && this.b < other.a;
        }

        /** Does this start at or before other? Nondisjoint */
        public bool StartsBeforeNonDisjoint( Interval other )
        {
            return this.a <= other.a && this.b >= other.a;
        }

        /** Does this.a start after other.b? May or may not be disjoint */
        public bool StartsAfter( Interval other )
        {
            return this.a > other.a;
        }

        /** Does this start completely after other? Disjoint */
        public bool StartsAfterDisjoint( Interval other )
        {
            return this.a > other.b;
        }

        /** Does this start after other? NonDisjoint */
        public bool StartsAfterNonDisjoint( Interval other )
        {
            return this.a > other.a && this.a <= other.b; // this.b>=other.b implied
        }

        /** Are both ranges disjoint? I.e., no overlap? */
        public bool Disjoint( Interval other )
        {
            return StartsBeforeDisjoint( other ) || StartsAfterDisjoint( other );
        }

        /** Are two intervals adjacent such as 0..41 and 42..42? */
        public bool Adjacent( Interval other )
        {
            return this.a == other.b + 1 || this.b == other.a - 1;
        }

        public bool ProperlyContains( Interval other )
        {
            return other.a >= this.a && other.b <= this.b;
        }

        /** Return the interval computed from combining this and other */
        public Interval Union( Interval other )
        {
            return Interval.FromBounds( Math.Min( a, other.a ), Math.Max( b, other.b ) );
        }

        /** Return the interval in common between this and o */
        public Interval Intersection( Interval other )
        {
            return Interval.FromBounds( Math.Max( a, other.a ), Math.Min( b, other.b ) );
        }

        /** Return the interval with elements from this not in other;
         *  other must not be totally enclosed (properly contained)
         *  within this, which would result in two disjoint intervals
         *  instead of the single one returned by this method.
         */
        public Interval? DifferenceNotProperlyContained( Interval other )
        {
            Interval? diff = null;

            // other.a to left of this.a (or same)
            if ( other.StartsBeforeNonDisjoint( this ) )
            {
                diff = Interval.FromBounds( Math.Max( this.a, other.b + 1 ),
                                       this.b );
            }
            // other.a to right of this.a
            else if ( other.StartsAfterNonDisjoint( this ) )
            {
                diff = Interval.FromBounds( this.a, other.a - 1 );
            }

            return diff;
        }

        public override string ToString()
        {
            return a + ".." + b;
        }
    }
}
