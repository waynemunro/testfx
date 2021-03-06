// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;
    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable SA1649 // File name must match first type name

    /// <summary>
    /// Internal implementation of TestContext exposed to the user.
    /// The virtual string properties of the TestContext are retreived from the property dictionary
    /// like GetProperty<string>("TestName") or GetProperty<string>("FullyQualifiedTestClassName");
    /// </summary>
    public class TestContextImplementation : UTF.TestContext, ITestContext
    {
        /// <summary>
        /// List of result files associated with the test
        /// </summary>
        private IList<string> testResultFiles;

        /// <summary>
        /// Properties
        /// </summary>
        private IDictionary<string, object> properties;

        /// <summary>
        /// Unit test outcome
        /// </summary>
        private UTF.UnitTestOutcome outcome;

        /// <summary>
        /// Writer on which the messages given by the user should be written
        /// </summary>
        private StringWriter stringWriter;

        /// <summary>
        /// Specifies whether the writer is disposed or not
        /// </summary>
        private bool stringWriterDisposed = false;

        /// <summary>
        /// Test Method
        /// </summary>
        private ITestMethod testMethod;

        /// <summary>
        /// DB connection for test context
        /// </summary>
        private DbConnection dbConnection;

        /// <summary>
        /// Data row for TestContext
        /// </summary>
        private DataRow dataRow;

        // Summary:
        //     Initializes a new instance of an object that derives from the Microsoft.VisualStudio.TestTools.UnitTesting.TestContext
        //     class.
        public TestContextImplementation(ITestMethod testMethod, StringWriter stringWriter, IDictionary<string, object> properties)
        {
            Debug.Assert(testMethod != null, "TestMethod is not null");
            Debug.Assert(stringWriter != null, "StringWriter is not null");
            Debug.Assert(properties != null, "properties is not null");

            this.testMethod = testMethod;
            this.stringWriter = stringWriter;
            this.properties = new Dictionary<string, object>(properties);

            this.InitializeProperties();

            this.testResultFiles = new List<string>();
        }

        #region TestContext impl

        // Summary:
        //     You can use this property in a TestCleanup method to determine the outcome
        //     of a test that has run.
        //
        // Returns:
        //     A Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome that states
        //     the outcome of a test that has run.
        public override UTF.UnitTestOutcome CurrentTestOutcome
        {
            get
            {
                return this.outcome;
            }
        }

        // Summary:
        //     When overridden in a derived class, gets the current data connection when
        //     the test is used for data driven testing.
        //
        // Returns:
        //     A System.Data.Common.DbConnection object.
        public override DbConnection DataConnection
        {
            get
            {
                return this.dbConnection;
            }
        }

        // Summary:
        //     When overridden in a derived class, gets the current data row when test is
        //     used for data driven testing.
        //
        // Returns:
        //     A System.Data.DataRow object.
        public override DataRow DataRow
        {
            get
            {
                return this.dataRow;
            }
        }

        // Summary:
        //     When overridden in a derived class, gets the test properties.
        //
        // Returns:
        //     An System.Collections.IDictionary object that contains key/value pairs that
        //     represent the test properties.
        public override IDictionary Properties
        {
            get
            {
                return this.properties as IDictionary;
            }
        }

        /// <summary>
        /// Gets base directory for the test run, under which deployed files and result files are stored.
        /// </summary>
        public override string TestRunDirectory
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestRunDirectory);
            }
        }

        /// <summary>
        /// Gets directory for files deployed for the test run. Typically a subdirectory of <see cref="TestRunDirectory"/>.
        /// </summary>
        public override string DeploymentDirectory
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.DeploymentDirectory);
            }
        }

        /// <summary>
        /// Gets base directory for results from the test run. Typically a subdirectory of <see cref="TestRunDirectory"/>.
        /// </summary>
        public override string ResultsDirectory
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.ResultsDirectory);
            }
        }

        /// <summary>
        /// Gets directory for test run result files. Typically a subdirectory of <see cref="ResultsDirectory"/>.
        /// </summary>
        public override string TestRunResultsDirectory
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestRunResultsDirectory);
            }
        }

        /// <summary>
        /// Gets directory for test result files.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", Justification = "TestResultsDirectory is what we need.")]
        public override string TestResultsDirectory
        {
            get
            {
                // In MSTest, it is actually "In\697105f7-004f-42e8-bccf-eb024870d3e9\User1", but
                // we are setting it to "In" only because MSTest does not create this directory.
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestResultsDirectory);
            }
        }

        /// <inheritdoc/>
        public override string TestDir
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestDir);
            }
        }

        /// <inheritdoc/>
        public override string TestDeploymentDir
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestDeploymentDir);
            }
        }

        /// <inheritdoc/>
        public override string TestLogsDir
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestLogsDir);
            }
        }

        /// <inheritdoc/>
        public override string FullyQualifiedTestClassName
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.FullyQualifiedTestClassName);
            }
        }

        /// <inheritdoc/>
        public override string TestName
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestName);
            }
        }

        public UTF.TestContext Context
        {
            get
            {
                return this as UTF.TestContext;
            }
        }

        // Summary:
        //     When overridden in a derived class, adds a file name to the list in TestResult.ResultFileNames.
        //
        // Parameters:
        //   fileName:
        //     The file name to add.
        public override void AddResultFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException(Resource.Common_CannotBeNullOrEmpty, "fileName");
            }

            this.testResultFiles.Add(Path.GetFullPath(fileName));
        }

        // Summary:
        //     When overridden in a derived class, begins a timer with the specified name.
        //
        // Parameters:
        //   timerName:
        //     The name of the timer to begin.
        public override void BeginTimer(string timerName)
        {
            throw new NotSupportedException();
        }

        // Summary:
        //     When overridden in a derived class, ends a timer with the specified name
        //
        // Parameters:
        //   timerName:
        //     The name of the timer to end.
        public override void EndTimer(string timerName)
        {
            throw new NotSupportedException();
        }

        // Summary:
        //     When overridden in a derived class, used to write trace messages while the
        //     test is running.
        //
        // Parameters:
        //   message:
        //     The formatted string that contains the trace message.
        public override void WriteLine(string message)
        {
            if (this.stringWriterDisposed)
            {
                return;
            }

            try
            {
                var msg = message?.Replace("\0", "\\0");
                this.stringWriter.WriteLine(msg);
            }
            catch (ObjectDisposedException)
            {
                this.stringWriterDisposed = true;
            }
        }

        // Summary:
        //     When overridden in a derived class, used to write trace messages while the
        //     test is running.
        //
        // Parameters:
        //   format:
        //     The string that contains the trace message.
        //
        //   args:
        //     Arguments to add to the trace message.
        public override void WriteLine(string format, params object[] args)
        {
            if (this.stringWriterDisposed)
            {
                return;
            }

            try
            {
                string message = string.Format(CultureInfo.CurrentCulture, format?.Replace("\0", "\\0"), args);
                this.stringWriter.WriteLine(message);
            }
            catch (ObjectDisposedException)
            {
                this.stringWriterDisposed = true;
            }
        }

        /// <summary>
        /// Set the unit-test outcome
        /// </summary>
        /// <param name="outcome">The test outcome.</param>
        public void SetOutcome(UTF.UnitTestOutcome outcome)
        {
            this.outcome = ToUTF(outcome);
        }

        /// <summary>
        /// Returns whether property with parameter name is present or not
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyValue">The property value.</param>
        /// <returns>True if found.</returns>
        public bool TryGetPropertyValue(string propertyName, out object propertyValue)
        {
            if (this.properties == null)
            {
                propertyValue = null;
                return false;
            }

            return this.properties.TryGetValue(propertyName, out propertyValue);
        }

        /// <summary>
        /// Adds the parameter name/value pair to property bag
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyValue">The property value.</param>
        public void AddProperty(string propertyName, string propertyValue)
        {
            if (this.properties == null)
            {
                this.properties = new Dictionary<string, object>();
            }

            this.properties.Add(propertyName, propertyValue);
        }

        /// <summary>
        /// Result files attached
        /// </summary>
        /// <returns>Results files generated in run.</returns>
        public IList<string> GetResultFiles()
        {
            if (this.testResultFiles.Count() == 0)
            {
                return null;
            }

            List<string> results = this.testResultFiles.ToList();

            // clear the result files to handle data driven tests
            this.testResultFiles.Clear();

            return results;
        }

        #endregion

        #region internal methods

#pragma warning disable SA1201 // Elements must appear in the correct order

        /// <summary>
        /// Gets messages from the testContext writeLines
        /// </summary>
        internal string Messages
        {
            get { return this.stringWriter.ToString(); }
        }

#pragma warning restore SA1201 // Elements must appear in the correct order

        /// <summary>
        /// Clears the previous testContext writeline messages.
        /// </summary>
        internal void ClearMessages()
        {
            var sb = this.stringWriter.GetStringBuilder();
            sb.Remove(0, sb.Length);
        }

        /// <summary>
        /// Set connection for TestContext
        /// </summary>
        /// <param name="dbConnection">db Connection.</param>
        internal void SetDataConnection(DbConnection dbConnection)
        {
            this.dbConnection = dbConnection;
        }

        /// <summary>
        /// Set data row for particular run of Windows Store app.
        /// </summary>
        /// <param name="dataRow">data row</param>
        internal void SetDataRow(DataRow dataRow)
        {
            this.dataRow = dataRow;
        }

        #endregion

        /// <summary>
        /// Converts the parameter outcome to UTF outcome
        /// </summary>
        /// <param name="outcome">The UTF outcome.</param>
        /// <returns>test outcome</returns>
        private static UTF.UnitTestOutcome ToUTF(UTF.UnitTestOutcome outcome)
        {
            switch (outcome)
            {
                case UTF.UnitTestOutcome.Error:
                    {
                        return UTF.UnitTestOutcome.Error;
                    }

                case UTF.UnitTestOutcome.Failed:
                    {
                        return UTF.UnitTestOutcome.Failed;
                    }

                case UTF.UnitTestOutcome.Inconclusive:
                    {
                        return UTF.UnitTestOutcome.Inconclusive;
                    }

                case UTF.UnitTestOutcome.Passed:
                    {
                        return UTF.UnitTestOutcome.Passed;
                    }

                case UTF.UnitTestOutcome.Timeout:
                    {
                        return UTF.UnitTestOutcome.Timeout;
                    }

                case UTF.UnitTestOutcome.InProgress:
                    {
                        return UTF.UnitTestOutcome.InProgress;
                    }

                default:
                    {
                        Debug.Fail("Unknown outcome " + outcome);
                        return UTF.UnitTestOutcome.Unknown;
                    }
            }
        }

        /// <summary>
        /// Helper to safely fetch a property value.
        /// </summary>
        /// <param name="propertyName">Property Name</param>
        /// <returns>Property value</returns>
        private string GetStringPropertyValue(string propertyName)
        {
            object propertyValue = null;
            this.properties.TryGetValue(propertyName, out propertyValue);
            return propertyValue as string;
        }

        /// <summary>
        /// Helper to initialize the properties.
        /// </summary>
        private void InitializeProperties()
        {
            this.properties[TestContextPropertyStrings.FullyQualifiedTestClassName] = this.testMethod.FullClassName;
            this.properties[TestContextPropertyStrings.TestName] = this.testMethod.Name;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
#pragma warning disable SA1402 // File may only contain a single class

    /// <summary>
    /// Test Context Property Names.
    /// </summary>
    internal static class TestContextPropertyStrings
#pragma warning restore SA1402 // File may only contain a single class
    {
        public static readonly string TestRunDirectory = "TestRunDirectory";
        public static readonly string DeploymentDirectory = "DeploymentDirectory";
        public static readonly string ResultsDirectory = "ResultsDirectory";
        public static readonly string TestRunResultsDirectory = "TestRunResultsDirectory";
        public static readonly string TestResultsDirectory = "TestResultsDirectory";
        public static readonly string TestDir = "TestDir";
        public static readonly string TestDeploymentDir = "TestDeploymentDir";
        public static readonly string TestLogsDir = "TestLogsDir";

        public static readonly string FullyQualifiedTestClassName = "FullyQualifiedTestClassName";
        public static readonly string TestName = "TestName";
    }
}
