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

using System;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;

namespace MonoTests.System.Linq.Expressions
{
    [TestFixture]
    public class ExpressionTestField
    {
        public static string Foo = "foo";

        [Test]
        public void Arg1Null()
        {
            Assert.Throws<ArgumentNullException>(() => Expression.Field(null, "NoField"));
        }

        [Test]
        public void Arg2Null1()
        {
            Assert.Throws<ArgumentNullException>(() => Expression.Field(Expression.Constant(new MemberClass()), (string)null));
        }

        [Test]
        public void Arg2Null2()
        {
            Assert.Throws<ArgumentNullException>(() => Expression.Field(Expression.Constant(new MemberClass()), (FieldInfo)null));
        }

        [Test]
        public void CompileInstanceField()
        {
            var p = Expression.Parameter(typeof(Bar), "bar");
            var compiled = Expression.Lambda<Func<Bar, string>>
            (
                Expression.Field
                (
                    p, typeof(Bar).GetField
                    (
                        nameof(Bar.Baz), BindingFlags.Public | BindingFlags.Instance
                    )
                ), p
            ).Compile();

            Assert.AreEqual("baz", compiled(new Bar()));
        }

        [Test]
        public void CompileStaticField()
        {
            var compiled = Expression.Lambda<Func<string>>
            (
                Expression.Field
                (
                    null, GetType().GetField
                    (
                        "Foo", BindingFlags.Static | BindingFlags.Public
                    )
                )
            ).Compile();

            Assert.AreEqual("foo", compiled());
        }

        [Test]
        public void CompileStructInstanceField()
        {
            var p = Expression.Parameter(typeof(Gazonk), "gazonk");
            var compiled = Expression.Lambda<Func<Gazonk, string>>
            (
                Expression.Field(p, typeof(Gazonk).GetField(nameof(Gazonk.Zap))), p
            ).Compile();

            Assert.AreEqual("bang", compiled(new Gazonk("bang")));
        }

        [Test]
        public void InstanceField()
        {
            var expr = Expression.Field(Expression.Constant(new MemberClass()), "TestField1");
            Assert.AreEqual(ExpressionType.MemberAccess, expr.NodeType, "Field#01");
            Assert.AreEqual(typeof(int), expr.Type, "Field#02");
            Assert.AreEqual("value(MonoTests.System.Linq.Expressions.MemberClass).TestField1", expr.ToString(), "Field#03");
        }

        [Test]
        public void NoField()
        {
            Assert.Throws<ArgumentException>(() => Expression.Field(Expression.Constant(new MemberClass()), "NoField"));
        }

        [Test]
        public void StaticField1()
        {
            Assert.Throws<ArgumentException>
            (
                () => Expression.Field(Expression.Constant(new MemberClass()), "StaticField"));
        }

        [Test]
        public void StaticField2()
        {
            var expr = Expression.Field(null, MemberClass.GetStaticFieldInfo());
            Assert.AreEqual(ExpressionType.MemberAccess, expr.NodeType, "Field#07");
            Assert.AreEqual(typeof(int), expr.Type, "Field#08");
            Assert.AreEqual("MemberClass.StaticField", expr.ToString(), "Field#09");
        }

        [Test]
        [Category("NotDotNet")] // http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=339351
        public void StaticFieldWithInstanceArgument()
        {
            Assert.Throws<ArgumentException>
            (
                () => Expression.Field
                (
                    Expression.Parameter(GetType(), "t"),
                    GetType().GetField("Foo")
                )
            );
        }

        public struct Gazonk
        {
            public string Zap;

            public Gazonk(string zap)
            {
                Zap = zap;
            }
        }

        public class Bar
        {
            public string Baz;

            public Bar()
            {
                Baz = "baz";
            }
        }
    }
}