using System;
using NUnit.Framework;

namespace SmallGames.Tests
{
    /// <summary>缓动求值 + 通用对象池：两块完全不依赖 UnityEngine 运行时行为的纯逻辑。</summary>
    public sealed class UiToolkitTests
    {
        [Test]
        public void EaseValue_端点必须是0和1()
        {
            foreach (EaseKind ease in Enum.GetValues(typeof(EaseKind)))
            {
                Assert.AreEqual(0f, UITween.EaseValue(ease, 0f), 1e-5f, ease.ToString());
                Assert.AreEqual(1f, UITween.EaseValue(ease, 1f), 1e-5f, ease.ToString());
            }
        }

        [Test]
        public void EaseValue_超出0到1会被夹紧()
        {
            Assert.AreEqual(0f, UITween.EaseValue(EaseKind.Linear, -3f), 1e-5f);
            Assert.AreEqual(1f, UITween.EaseValue(EaseKind.Linear, 9f), 1e-5f);
        }

        [Test]
        public void EaseValue_OutBack会过冲_OutQuad不会()
        {
            Assert.Greater(UITween.EaseValue(EaseKind.OutBack, 0.6f), 1f);
            Assert.LessOrEqual(UITween.EaseValue(EaseKind.OutQuad, 0.6f), 1f);
        }

        [Test]
        public void EaseValue_单调不能回头()
        {
            var prev = -1f;
            for (var i = 0; i <= 20; i++)
            {
                var t = i / 20f;
                var v = UITween.EaseValue(EaseKind.OutCubic, t);
                Assert.GreaterOrEqual(v, prev - 1e-6f, "OutCubic 在 t=" + t + " 处回头了");
                prev = v;
            }
        }

        private sealed class Bag
        {
            public bool Live;
        }

        [Test]
        public void ObjectPool_借出回收后复用同一个对象()
        {
            var made = 0;
            var pool = new ObjectPool<Bag>(
                () =>
                {
                    made++;
                    return new Bag();
                },
                b => b.Live = true,
                b => b.Live = false);

            var first = pool.Take();
            Assert.IsNotNull(first);
            Assert.IsTrue(first.Live);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountFree);

            Assert.IsTrue(pool.Return(first));
            Assert.IsFalse(first.Live);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountFree);

            var second = pool.Take();
            Assert.AreSame(first, second, "池里明明有空闲对象，不该再 new");
            Assert.AreEqual(1, made);
        }

        [Test]
        public void ObjectPool_重复回收只会成功一次()
        {
            var pool = new ObjectPool<Bag>(() => new Bag());
            var item = pool.Take();

            Assert.IsTrue(pool.Return(item));
            Assert.IsFalse(pool.Return(item), "同一个对象被回收两次，后面 reset 时会出双份");
            Assert.AreEqual(1, pool.CountFree);
        }

        [Test]
        public void ObjectPool_没借出去过的对象不能回收()
        {
            var pool = new ObjectPool<Bag>(() => new Bag());
            Assert.IsFalse(pool.Return(new Bag()));
            Assert.IsFalse(pool.Return(null));
        }

        [Test]
        public void ObjectPool_Prewarm预热但不算活跃()
        {
            var pool = new ObjectPool<Bag>(() => new Bag());
            pool.Prewarm(5);
            Assert.AreEqual(5, pool.CountFree);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(5, pool.CountAll);
        }

        [Test]
        public void ObjectPool_Adopt收编外部对象_在场对象不能重复收编()
        {
            var pool = new ObjectPool<Bag>(() => new Bag());
            var outside = new Bag();
            Assert.IsTrue(pool.Adopt(outside));
            Assert.AreSame(outside, pool.Take());

            var live = pool.Take();
            Assert.IsFalse(pool.Adopt(live), "还在外面的对象不能被收编到空闲区");
        }

        [Test]
        public void ObjectPool_Clear清理全部并可选销毁()
        {
            var disposed = 0;
            var pool = new ObjectPool<Bag>(() => new Bag());
            pool.Prewarm(3);
            var live = pool.Take();

            pool.Clear(b => disposed++);
            Assert.AreEqual(4, disposed, "3 个空闲 + 1 个活跃都要处理到");
            Assert.AreEqual(0, pool.CountAll);
            Assert.IsFalse(pool.Return(live));
        }
    }
}
