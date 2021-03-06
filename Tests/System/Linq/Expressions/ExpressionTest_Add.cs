﻿#if LESSTHAN_NET35
extern alias nunitlinq;
#endif

// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//
// Authors:
//      Federico Di Gregorio <fog@initd.org>
//      Jb Evain <jbevain@novell.com>

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using Tests.Helpers;
using Theraot;

namespace MonoTests.System.Linq.Expressions
{
    [TestFixture]
    public class ExpressionTestAdd
    {
        [Test]
        public void AddDecimals()
        {
            const string nameLeft = "l";
            const string nameRight = "r";
            const decimal valueLeft = 1m;
            const decimal valueRight = 1m;
            const decimal result = valueLeft + valueRight;
            var method = typeof(decimal).GetMethod("op_Addition", new[] { typeof(decimal), typeof(decimal) });

            var parameterLeft = Expression.Parameter(typeof(decimal), nameLeft);
            var parameterRight = Expression.Parameter(typeof(decimal), nameRight);

            var binaryExpression = Expression.Add(parameterLeft, parameterRight);
            Assert.IsFalse(binaryExpression.IsLifted);
            Assert.IsFalse(binaryExpression.IsLiftedToNull);
            Assert.AreEqual(typeof(decimal), binaryExpression.Type);
            Assert.AreEqual(method, binaryExpression.Method);

            var compiled = Expression.Lambda<Func<decimal, decimal, decimal>>(binaryExpression, parameterLeft, parameterRight).Compile();

            Assert.AreEqual(result, compiled(valueLeft, valueRight));
        }

        [Test]
        public void AddLifted()
        {
            var type = typeof(int?);

            var additionExpression = Expression.Add
            (
                Expression.Constant(null, type),
                Expression.Constant(null, type)
            );

            Assert.AreEqual(type, additionExpression.Type);
            Assert.IsTrue(additionExpression.IsLifted);
            Assert.IsTrue(additionExpression.IsLiftedToNull);
        }

        [Test]
        public void AddLiftedDecimals()
        {
            const string nameLeft = "l";
            const string nameRight = "r";
            const decimal valueLeft = 1m;
            const decimal valueRight = 1m;
            const decimal result = valueLeft + valueRight;
            var type = typeof(decimal?);
            var method = typeof(decimal).GetMethod("op_Addition", new[] { typeof(decimal), typeof(decimal) });

            var parameterLeft = Expression.Parameter(type, nameLeft);
            var parameterRight = Expression.Parameter(type, nameRight);

            var binaryExpression = Expression.Add(parameterLeft, parameterRight);
            Assert.IsTrue(binaryExpression.IsLifted);
            Assert.IsTrue(binaryExpression.IsLiftedToNull);
            Assert.AreEqual(type, binaryExpression.Type);
            Assert.AreEqual(method, binaryExpression.Method);

            var compiled = Expression.Lambda<Func<decimal?, decimal?, decimal?>>(binaryExpression, parameterLeft, parameterRight).Compile();

            Assert.AreEqual(result, compiled(valueLeft, valueRight));
            Assert.AreEqual(null, compiled(valueLeft, null));
            Assert.AreEqual(null, compiled(null, valueRight));
            Assert.AreEqual(null, compiled(null, null));
        }

        [Test]
        public void AddNotLifted()
        {
            const int valueLeft = 1;
            const int valueRight = 1;
            var type = typeof(int);

            var binaryExpression = Expression.Add
            (
                Expression.Constant(valueLeft, type),
                Expression.Constant(valueRight, type)
            );

            Assert.AreEqual(type, binaryExpression.Type);
            Assert.IsFalse(binaryExpression.IsLifted);
            Assert.IsFalse(binaryExpression.IsLiftedToNull);
        }

        [Test]
        public void AddStrings()
        {
            const string nameLeft = "l";
            const string nameRight = "r";
            const string valueLeft = "foo";
            const string valueRight = "bar";
            const string result = valueLeft + valueRight;
            var type = typeof(string);
            var method = new Func<object, object, string>(string.Concat).GetMethodInfo();

            var parameterLeft = Expression.Parameter(type, nameLeft);
            var parameterRight = Expression.Parameter(type, nameRight);

            var binaryExpression = Expression.Add(parameterLeft, parameterRight, method);
            Assert.IsFalse(binaryExpression.IsLifted);
            Assert.IsFalse(binaryExpression.IsLiftedToNull);
            Assert.AreEqual(type, binaryExpression.Type);
            Assert.AreEqual(method, binaryExpression.Method);

            var compiled = Expression.Lambda<Func<string, string, string>>(binaryExpression, parameterLeft, parameterRight).Compile();

            Assert.AreEqual(string.Empty, compiled(null, null));
            Assert.AreEqual(result, compiled(valueLeft, valueRight));
        }

        [Test]
        public void AddTestNullable()
        {
            const string nameLeft = "a";
            const string nameRight = "b";
            const int valueLeft = 1;
            const int valueRight = 2;
            const decimal result = valueLeft + valueRight;
            var type = typeof(int?);

            var parameterLeft = Expression.Parameter(type, nameLeft);
            var parameterRight = Expression.Parameter(type, nameRight);
            var lambda = Expression.Lambda<Func<int?, int?, int?>>(Expression.Add(parameterLeft, parameterRight), parameterLeft, parameterRight);

            var binaryExpression = lambda.Body as BinaryExpression;
            Assert.IsNotNull(binaryExpression);
            Assert.AreEqual(type, binaryExpression.Type);
            Assert.IsTrue(binaryExpression.IsLifted);
            Assert.IsTrue(binaryExpression.IsLiftedToNull);

            var compiled = lambda.Compile();

            Assert.AreEqual(null, compiled(valueLeft, null), "a1");
            Assert.AreEqual(null, compiled(null, null), "a2");
            Assert.AreEqual(null, compiled(null, valueRight), "a3");
            Assert.AreEqual(result, compiled(valueLeft, valueRight), "a4");
        }

        [Test]
        public void Arg1Null()
        {
            const int value = 1;

            // ReSharper disable once AssignNullToNotNullAttribute
            AssertEx.Throws<ArgumentNullException>(() => Expression.Add(null, Expression.Constant(value)));
        }

        [Test]
        public void Arg2Null()
        {
            const int value = 1;

            // ReSharper disable once AssignNullToNotNullAttribute
            AssertEx.Throws<ArgumentNullException>(() => Expression.Add(Expression.Constant(value), null));
        }

        [Test]
        public void ArgTypesDifferent()
        {
            const int valueLeft = 1;
            const double valueRight = 2.0;

            AssertEx.Throws<InvalidOperationException>(() => Expression.Add(Expression.Constant(valueLeft), Expression.Constant(valueRight)));
        }

        [Test]
        public void Boolean()
        {
            const bool valueLeft = true;
            const bool valueRight = false;

            AssertEx.Throws<InvalidOperationException>(() => Expression.Add(Expression.Constant(valueLeft), Expression.Constant(valueRight)));
        }

        [Test]
        public void CompileAdd()
        {
            const string nameLeft = "l";
            const string nameRight = "r";

            var input = new[]
            {
                (Left: 6, Right: 6),
                (Left: -1, Right: 1),
                (Left: 1, Right: -3)
            };

            var instances = input.Select(value => (value.Left, value.Right, Result: value.Left + value.Right));

            var type = typeof(int);

            var parameterLeft = Expression.Parameter(type, nameLeft);
            var parameterRight = Expression.Parameter(type, nameRight);
            var lambda = Expression.Lambda<Func<int, int, int>>(Expression.Add(parameterLeft, parameterRight), parameterLeft, parameterRight);

            var binaryExpression = lambda.Body as BinaryExpression;
            Assert.IsNotNull(binaryExpression);
            Assert.AreEqual(type, binaryExpression.Type);
            Assert.IsFalse(binaryExpression.IsLifted);
            Assert.IsFalse(binaryExpression.IsLiftedToNull);

            var compiled = lambda.Compile();

            foreach (var (left, right, result) in instances)
            {
                Assert.AreEqual(result, compiled(left, right));
            }
        }

        [Test]
        public void NoOperatorClass()
        {
            AssertEx.Throws<InvalidOperationException>(() => Expression.Add(Expression.Constant(new NoOpClass()), Expression.Constant(new NoOpClass())));
        }

        [Test]
        public void Nullable()
        {
            int? valueLeft = 1;
            int? valueRight = 2;
            var type = typeof(int?);

            var binaryExpression = Expression.Add
            (
                Expression.Constant(valueLeft, type),
                Expression.Constant(valueRight, type)
            );
            Assert.AreEqual(ExpressionType.Add, binaryExpression.NodeType, "Add#05");
            Assert.AreEqual(type, binaryExpression.Type, "Add#06");
            Assert.IsNull(binaryExpression.Method, "Add#07");
            Assert.AreEqual($"({valueLeft} + {valueRight})", binaryExpression.ToString(), "Add#08");
        }

        [Test]
        public void Numeric()
        {
            const int valueLeft = 1;
            const int valueRight = 2;
            var type = typeof(int);

            var binaryExpression = Expression.Add(Expression.Constant(valueLeft), Expression.Constant(valueRight));
            Assert.AreEqual(ExpressionType.Add, binaryExpression.NodeType, "Add#01");
            Assert.AreEqual(type, binaryExpression.Type, "Add#02");
            Assert.IsNull(binaryExpression.Method, "Add#03");
            Assert.AreEqual($"({valueLeft} + {valueRight})", binaryExpression.ToString(), "Add#04");
        }

        [Test]
        public void TestMethodAddition()
        {
            const int valueLeft = 1;
            const int valueRight = 2;
            var result = S.MyAdder(valueLeft, valueRight);

            var binaryExpression = Expression.Add
            (
                Expression.Constant(valueLeft),
                Expression.Constant(valueRight),
                new Func<int, int, int>(S.MyAdder).GetMethodInfo()
            );
            var lambda = Expression.Lambda<Func<int>>(binaryExpression);

            var compiled = lambda.Compile();
            Assert.AreEqual(result, compiled());
        }

        [Test]
        public void UserDefinedAdd()
        {
            const string nameLeft = "l";
            const string nameRight = "r";

            var input = new[]
            {
                (Left: 21, Right: 21),
                (Left: 1, Right: -1)
            };

            var instances = input.Select(value => (value.Left, value.Right, Result: value.Left + value.Right));

            var type = typeof(Slot);

            var parameterLeft = Expression.Parameter(type, nameLeft);
            var parameterRight = Expression.Parameter(type, nameRight);

            var binaryExpression = Expression.Add(parameterLeft, parameterRight);

            Assert.IsFalse(binaryExpression.IsLifted);
            Assert.IsFalse(binaryExpression.IsLiftedToNull);
            Assert.AreEqual(type, binaryExpression.Type);

            var compiled = Expression.Lambda<Func<Slot, Slot, Slot>>(binaryExpression, parameterLeft, parameterRight).Compile();

            foreach (var (left, right, result) in instances)
            {
                Assert.AreEqual(new Slot(result), compiled(new Slot(left), new Slot(right)));
            }
        }

        [Test]
        public void UserDefinedAddLifted()
        {
            const string nameLeft = "l";
            const string nameRight = "r";

            const int valueLeft = 21;
            const int valueRight = 21;

            const int result = valueLeft + valueRight;

            var type = typeof(Slot?);

            var parameterLeft = Expression.Parameter(type, nameLeft);
            var parameterRight = Expression.Parameter(type, nameRight);

            var binaryExpression = Expression.Add(parameterLeft, parameterRight);

            Assert.IsTrue(binaryExpression.IsLifted);
            Assert.IsTrue(binaryExpression.IsLiftedToNull);
            Assert.AreEqual(type, binaryExpression.Type);

            var compiled = Expression.Lambda<Func<Slot?, Slot?, Slot?>>(binaryExpression, parameterLeft, parameterRight).Compile();

            Assert.AreEqual(null, compiled(null, null));
            Assert.AreEqual((Slot?)new Slot(result), compiled(new Slot(valueLeft), new Slot(valueRight)));
        }

        [Test]
        public void UserDefinedClass()
        {
            var valueLeft = new OpClass();
            var valueRight = new OpClass();
            var result = valueLeft + valueRight;

            var type = typeof(OpClass);

            // We can use the simplest version of GetMethod because we already know only one
            // exists in the very simple class we're using for the tests.
            var method = type.GetMethod("op_Addition");

            var binaryExpression = Expression.Add(Expression.Constant(valueLeft), Expression.Constant(valueRight));
            Assert.AreEqual(ExpressionType.Add, binaryExpression.NodeType, "Add#09");
            Assert.AreEqual(type, binaryExpression.Type, "Add#10");
            Assert.AreEqual(method, binaryExpression.Method, "Add#11");
            Assert.AreEqual
            (
                $"(value({type.FullName}) + value({type.FullName}))",
                binaryExpression.ToString(),
                "Add#13"
            );

            var lambda = Expression.Lambda<Func<OpClass>>(binaryExpression);

            var compiled = lambda.Compile();
            Assert.AreEqual(result, compiled());
        }

        [Test]
        public void UserDefinedFromNullableToNullableAdd()
        {
            const string nameLeft = "l";
            const string nameRight = "r";

            var input = new (int? Left, int? Right)[]
            {
                (Left: null, Right: null),
                (Left: 2, Right: null),
                (Left: null, Right: 2),
                (Left: 2, Right: 2),
                (Left: 2, Right: -2)
            };

            var instances = input.Select(value => (value.Left, value.Right, Result: value.Left + value.Right));

            var type = typeof(SlotFromNullableToNullable?);

            var parameterLeft = Expression.Parameter(type, nameLeft);
            var parameterRight = Expression.Parameter(type, nameRight);

            var binaryExpression = Expression.Add(parameterLeft, parameterRight);

            Assert.IsFalse(binaryExpression.IsLifted);
            Assert.IsFalse(binaryExpression.IsLiftedToNull);
            Assert.AreEqual(type, binaryExpression.Type);
            Assert.IsNotNull(binaryExpression.Method);

            var compiled = Expression.Lambda<Func<SlotFromNullableToNullable?, SlotFromNullableToNullable?, SlotFromNullableToNullable?>>(binaryExpression, parameterLeft, parameterRight).Compile();

            foreach (var (left, right, result) in instances)
            {
                Assert.AreEqual
                (
                    result == null ? default(SlotFromNullableToNullable?) : new SlotFromNullableToNullable(result.Value),
                    compiled
                    (
                        left == null ? default(SlotFromNullableToNullable?) : new SlotFromNullableToNullable(left.Value),
                        right == null ? default(SlotFromNullableToNullable?) : new SlotFromNullableToNullable(right.Value)
                    )
                );
            }
        }

        [Test]
        public void UserDefinedToNullableAdd()
        {
            const string nameLeft = "l";
            const string nameRight = "r";

            var input = new[]
            {
                (Left: 2, Right: 2),
                (Left: 2, Right: -2)
            };

            var instances = input.Select(value => (value.Left, value.Right, Result: value.Left + value.Right));

            var parameterLeft = Expression.Parameter(typeof(SlotToNullable), nameLeft);
            var parameterRight = Expression.Parameter(typeof(SlotToNullable), nameRight);

            var binaryExpression = Expression.Add(parameterLeft, parameterRight);

            Assert.IsFalse(binaryExpression.IsLifted);
            Assert.IsFalse(binaryExpression.IsLiftedToNull);
            Assert.AreEqual(typeof(SlotToNullable?), binaryExpression.Type);
            Assert.IsNotNull(binaryExpression.Method);

            var compiled = Expression.Lambda<Func<SlotToNullable, SlotToNullable, SlotToNullable?>>(binaryExpression, parameterLeft, parameterRight).Compile();

            foreach (var (left, right, result) in instances)
            {
                Assert.AreEqual
                (
                    (SlotToNullable?)new SlotToNullable(result),
                    compiled(new SlotToNullable(left), new SlotToNullable(right))
                );
            }
        }

        [Test]
        public void UserDefinedToNullableAddFromNullable()
        {
            const string nameLeft = "l";
            const string nameRight = "r";

            AssertEx.Throws<InvalidOperationException>
            (
                () => Expression.Add
                (
                    Expression.Parameter(typeof(SlotToNullable?), nameLeft),
                    Expression.Parameter(typeof(SlotToNullable?), nameRight)
                )
            );
        }

        private struct Slot
        {
            private readonly int _value;

            public Slot(int value)
            {
                _value = value;
            }

            public static Slot operator +(Slot a, Slot b)
            {
                return new Slot(a._value + b._value);
            }
        }

        private struct SlotFromNullableToNullable
        {
            private readonly int _value;

            public SlotFromNullableToNullable(int value)
            {
                _value = value;
            }

            public static SlotFromNullableToNullable? operator +(SlotFromNullableToNullable? a, SlotFromNullableToNullable? b)
            {
                return a.HasValue && b.HasValue ? (SlotFromNullableToNullable?)new SlotFromNullableToNullable(a.Value._value + b.Value._value) : null;
            }
        }

        private struct SlotToNullable
        {
            private readonly int _value;

            public SlotToNullable(int value)
            {
                _value = value;
            }

            public static SlotToNullable? operator +(SlotToNullable a, SlotToNullable b)
            {
                return new SlotToNullable(a._value + b._value);
            }
        }

        public static class S
        {
            public static int MyAdder(int a, int b)
            {
                No.Op(a);
                No.Op(b);
                return 1000;
            }
        }
    }
}