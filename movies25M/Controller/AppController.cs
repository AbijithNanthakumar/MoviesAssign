using System;
using System.Diagnostics;
using Model;
using View;

namespace Controller
{
    public class AppController
    {
        private readonly ConsoleView _view = new ConsoleView();

        public void Run()
        {
            _view.ShowHeader();

            string datasetFolder = _view.AskDatasetFolder();
            if (string.IsNullOrWhiteSpace(datasetFolder))
            {
                _view.ShowMessage("Dataset path cannot be empty. Exiting...");
                return;
            }

            bool useMultithreading = _view.AskUseMultithreading();

            _view.ShowMessage("Loading dataset...");
            var totalTimer = Stopwatch.StartNew();
            MovieLensData data;
            try
            {
                data = MovieLensData.LoadFromFolder(datasetFolder);
            }
            catch (Exception ex)
            {
                _view.ShowMessage($"Error loading dataset: {ex.Message}");
                return;
            }

            string root = "Output";
            string modeFolder = useMultithreading ? "withMultithreading" : "withoutMultithreading";
            string outputFolder = System.IO.Path.Combine(root, modeFolder);

            _view.ShowMessage($"Generating reports ? {outputFolder}");
            var reportTimer = Stopwatch.StartNew();

            var generator = new ReportGenerator(data);
            if (useMultithreading)
                generator.GenerateAllReportsMultiThreaded(outputFolder);
            else
                generator.GenerateAllReportsSingleThreaded(outputFolder);

            reportTimer.Stop();
            totalTimer.Stop();

            _view.ShowExecutionTime(reportTimer.Elapsed, totalTimer.Elapsed);
            _view.ShowMessage($"Reports stored in {outputFolder}");
            _view.ShowMessage("Press Enter to exit...");
            Console.ReadLine();
        }
    }
}
