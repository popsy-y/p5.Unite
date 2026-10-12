using System;
using NUnit.Framework;
using Unite.Drawing;
using UnityEngine;

namespace Unite.Tests
{
    public sealed class ShaderLibraryTests
    {
        [Test]
        public void DefaultLibraryIsAvailable()
        {
            Assert.That(
                UniteShaderLibrary.Instance,
                Is.Not.Null,
                $"The shader library asset was not found at '{UniteShaderLibrary.AssetPath}'."
            );
        }

        [Test]
        public void EveryDrawingModeHasAShader()
        {
            var library = UniteShaderLibrary.Instance;
            Assert.That(library, Is.Not.Null);

            foreach (DrawingMode mode in Enum.GetValues(typeof(DrawingMode)))
            {
                Assert.That(
                    library.GetShader(mode),
                    Is.Not.Null,
                    $"No shader is assigned for {mode}."
                );
            }
        }

        [Test]
        public void AssignedShadersUseTheExpectedShaderNames()
        {
            var library = UniteShaderLibrary.Instance;
            Assert.That(library, Is.Not.Null);

            foreach (DrawingMode mode in Enum.GetValues(typeof(DrawingMode)))
            {
                var shader = library.GetShader(mode);
                Assert.That(shader, Is.Not.Null, $"No shader is assigned for {mode}.");
                Assert.That(
                    shader.name,
                    Is.EqualTo(UniteShaderLibrary.ShaderNameFor(mode)),
                    $"The shader assigned for {mode} is not the expected one."
                );
            }
        }
    }
}
