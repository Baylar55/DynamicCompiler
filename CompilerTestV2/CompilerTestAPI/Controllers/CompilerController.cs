using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using System.Reflection;
namespace CompilerTestAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompilerController : ControllerBase
    {
        [HttpPost,]
        public ActionResult<string> CompileAndRun([FromBody] CodeRequest request)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var code = request.Code;
                
                var compilationOptions = new CSharpCompilationOptions(OutputKind.ConsoleApplication);

                var references = new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
                };

                var syntaxTree = SyntaxFactory.ParseSyntaxTree(code);
                var compilation = CSharpCompilation.Create("LibraryAssembly")
                    .WithOptions(compilationOptions)
                    .AddReferences(references)
                    .AddSyntaxTrees(syntaxTree);

                using var ms = new MemoryStream();
                var emitResult = compilation.Emit(ms);

                if (!emitResult.Success)
                {
                    var errors = emitResult.Diagnostics.Select(diagnostic => diagnostic.ToString());
                    return "Compilation Error: " + string.Join(", ", errors);
                }
                else
                {
                    ms.Seek(0, SeekOrigin.Begin);

                    var assembly = Assembly.Load(ms.ToArray());

                    using (var sw = new StringWriter())
                    {
                        Console.SetOut(sw);

                        var mainMethods = assembly.GetTypes()
                            .SelectMany(t => t.GetMethods())
                            .Where(m => m.Name == "Main" && m.GetParameters().Length == 0);

                        foreach (var mainMethod in mainMethods)
                        {
                            mainMethod.Invoke(null, null);
                        }

                        stopwatch.Stop();
                        var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

                        var memoryUsageBytes = Process.GetCurrentProcess().WorkingSet64;
                        var memoryUsageMB = Math.Round(memoryUsageBytes / (1024.0 * 1024.0), 2);

                        return $"Output: {sw.ToString().Trim()}, Elapsed Time: {elapsedMilliseconds}ms, Memory Usage: {memoryUsageMB} MB";
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }
    public class CodeRequest
    {
        public required string Code { get; set; }
    }
}
