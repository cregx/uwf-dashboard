/**
 * The application was developed by Christoph Regner.
 * On the web: https://www.cregx.de
 * For further information, please refer to the attached LICENSE.md
 * 
 * The MIT License (MIT)
 * Copyright (c) 2020-2021 Christoph Regner
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;                         // String culture, National Language Support.
using Microsoft.Management.Infrastructure;          // Connecting to WMI (faster than with System.Management).
using Microsoft.Management.Infrastructure.Generic;  // Pending asynchronous operations (CIM).
using Microsoft.Management.Infrastructure.Options;  // Session options (CIM).
using System.Resources;                             // Access ressource files (*.resx).
using System.Threading.Tasks;                       // Tasks.
using System.Security.Principal;                    // Administrator rights.
using System.Windows.Media;                         // Brush.
using System.Reflection;                            // Assembly.
using System.Threading;                             // Tasks.
using MaterialDesignThemes.Wpf;                     // UI Design.
using System.Diagnostics;                           // Testing.
using System.Linq;                                  // e.g. for First() in EnumerateInstances()(CIM).
using System.ComponentModel;                        // Databinding.
using System.Windows.Data;
using System.Windows.Navigation;                    // Hyperlinks.

namespace Cregx.Uwf.Dashboard
{
    public struct UWFFilterMethodName
    {
        public const string Enable = "enable";
        public const string Disable = "disable";
        public const string RestartPC = "RestartSystem";
        public const string ResetFilter = "ResetSettings";
        public const string ShutdownPC = "ShutdownSystem";
    }

    /// <summary>
    /// Class with helper methods / functions.
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Checks if the application is running as an administrator.
        /// 
        /// Based on https://github.com/vinaypamnani/wmie2
        /// Under the MIT License Copyright(c) 2019 vinaypamnani.
        /// Modified @cregx 11/2020
        /// 
        /// </summary>
        /// <returns>True if application is running as an Administrator.</returns>
        public static bool IsAdministrator()
        {
            WindowsIdentity userIdentity = null;
            try
            {
                userIdentity = WindowsIdentity.GetCurrent();
                WindowsPrincipal userPrincipal = new WindowsPrincipal(userIdentity);

                if (userPrincipal.IsInRole(WindowsBuiltInRole.Administrator))
                    return true;

                return false;
            }
            catch (Exception)
            {
                // MessageBox.Show("Failed to determine if Application is running as Administrator: " + ex.Message);
                return false;
            }
            finally
            {
                if (userIdentity != null)
                {
                    userIdentity.Dispose();
                }
            }
        }

        #region Return codes structure for Async methods.
        /// <summary>
        /// Enum with return values for asynchronous methods.
        /// </summary>
        public enum AsyncReturnCode
        {
            successful = 0x0,
            cancelled = 0x1,
            error = 0x2
        }
        #endregion

        /// <summary>
        /// Returns a hexadecimal value of a decimal number.
        /// </summary>
        /// <param name="decimalValue">Decimal value as string.</param>
        /// <returns>Decimal value converted as hex string.</returns>
        public static string GetHexByDecimal(string decimalValue)
        {
            string strHex;
            try
            {
                strHex = String.Format("0x{0}", Convert.ToInt32(decimalValue).ToString("X"));
            }
            catch (Exception)
            {
                strHex = decimalValue;
            }
            return strHex;
        }
    }

    /// <summary>
    /// The central class for accessing the WMI UWF classes. 
    /// </summary>
    public class UnifiedWriteFilter
    {
        #region Class constructor
        /// <summary>
        /// Class constructor.
        /// </summary>
        public UnifiedWriteFilter()
        {
            // Class variable containing the last occurred error.
            lastError = ErrorCode.InitState;

            // Initialize all dictionary instances of the uwf classes.
            InitFilterDictionaries();
        }
        #endregion

        #region Container classes
        #region Container class (FilterProperty)
        /// <summary>
        /// Container class for the property dictionary.
        /// </summary>        
        public class FilterProperty
        {
            private string propertyValue;

            public FilterProperty(string propertyValue)
            {
                this.propertyValue = propertyValue ?? throw new ArgumentNullException(nameof(propertyValue));
            }

            public string GetPropertyValue()
            {
                return propertyValue;
            }

            public void SetPropertyValue(string value) => propertyValue = value;
        }
        #endregion

        #region Container class (ProtectedVolume)
        /// <summary>
        /// Container class for all data from the UWF_Volume class.
        /// </summary>
        public class ProtectedVolume
        {
            public bool isProtected { get; set; }
            public bool currentSession { get; set; }
            public bool commitPending { get; set; }
            public bool bindByDriveLetter { get; set; }
            public string driveLetter { get; set; }
            public string volumeName { get; set; }
            
            /// <summary>
            /// Class constructor
            /// </summary>
            public ProtectedVolume()
            {
                this.Clear();
            }

            public void Clear()
            { 
                isProtected = false;
                currentSession = false;
                commitPending = false;
                bindByDriveLetter = false;
                volumeName = "";
                driveLetter = "";
            }
        }
        #endregion
        #endregion

        #region Dictionaries with all collected filter properties.
        /// <summary>
        /// Dictionary with all properties of the UWF_Filter Class.
        /// </summary>
        public Dictionary<string, FilterProperty> UWF_Filters, UWF_Overlay, UWF_OverlayConfig, UWF_OverlayConfig2, UWF_Volume, UWF_Servicing, UWF_Servicing2;

        /// <summary>
        /// Initialises all required filter dictionaries.
        /// </summary>
        private void InitFilterDictionaries()
        {
            UWF_Filters = new Dictionary<string, FilterProperty>();
            AddToFilterDictionary(filterProperties: UWF_Filters, _WMI_PropertyName_CurrentEnabled);
            AddToFilterDictionary(filterProperties: UWF_Filters, _WMI_PropertyName_NextEnabled);
            AddToFilterDictionary(filterProperties: UWF_Filters, _WMI_PropertyName_HORMEnabled);
            AddToFilterDictionary(filterProperties: UWF_Filters, _WMI_PropertyName_Id);
            AddToFilterDictionary(filterProperties: UWF_Filters, _WMI_ProopertyName_ShutdownPending);
            // Error flags
            AddToFilterDictionary(filterProperties: UWF_Filters, _WMIQueryError_NativeErrorCode);
            AddToFilterDictionary(filterProperties: UWF_Filters, _WMIQueryError_HResult);
            AddToFilterDictionary(filterProperties: UWF_Filters, _WMIQueryError_Message);
            AddToFilterDictionary(filterProperties: UWF_Filters, _WMIQueryError_ErrorOccurred);
            AddToFilterDictionary(filterProperties: UWF_Filters, _WMIFailedConnectionTest);

            UWF_Overlay = new Dictionary<string, FilterProperty>();
            AddToFilterDictionary(filterProperties: UWF_Overlay, _WMI_PropertyName_AvailableSpace);
            AddToFilterDictionary(filterProperties: UWF_Overlay, _WMI_PropertyName_CriticalOverlayTreshold);
            AddToFilterDictionary(filterProperties: UWF_Overlay, _WMI_PropertyName_Id);
            AddToFilterDictionary(filterProperties: UWF_Overlay, _WMI_PropertyName_OverlayConsumption);
            AddToFilterDictionary(filterProperties: UWF_Overlay, _WMI_PropertyName_WarningOverlayThreshold);
            // Error flags
            AddToFilterDictionary(filterProperties: UWF_Overlay, _WMIQueryError_NativeErrorCode);
            AddToFilterDictionary(filterProperties: UWF_Overlay, _WMIQueryError_HResult);
            AddToFilterDictionary(filterProperties: UWF_Overlay, _WMIQueryError_Message);
            AddToFilterDictionary(filterProperties: UWF_Overlay, _WMIQueryError_ErrorOccurred);
            AddToFilterDictionary(filterProperties: UWF_Overlay, _WMIFailedConnectionTest);

            UWF_OverlayConfig = new Dictionary<string, FilterProperty>();
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig, _WMI_PropertyName_Type);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig, _WMI_PropertyName_MaximumSize);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig, _WMI_PropertyName_CurrentSession);
            // Error flags
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig, _WMIQueryError_NativeErrorCode);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig, _WMIQueryError_HResult);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig, _WMIQueryError_Message);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig, _WMIQueryError_ErrorOccurred);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig, _WMIFailedConnectionTest);

            UWF_OverlayConfig2 = new Dictionary<string, FilterProperty>();
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig2, _WMI_PropertyName_Type);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig2, _WMI_PropertyName_MaximumSize);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig2, _WMI_PropertyName_CurrentSession);
            // Error flags
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig2, _WMIQueryError_NativeErrorCode);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig2, _WMIQueryError_HResult);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig2, _WMIQueryError_Message);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig2, _WMIQueryError_ErrorOccurred);
            AddToFilterDictionary(filterProperties: UWF_OverlayConfig2, _WMIFailedConnectionTest);

            UWF_Volume = new Dictionary<string, FilterProperty>();
            AddToFilterDictionary(filterProperties: UWF_Volume, _WMI_PropertyName_BindByDriveLetter);
            AddToFilterDictionary(filterProperties: UWF_Volume, _WMI_PropertyName_CommitPending);
            AddToFilterDictionary(filterProperties: UWF_Volume, _WMI_PropertyName_CurrentSession);
            AddToFilterDictionary(filterProperties: UWF_Volume, _WMI_PropertyName_DriveLetter);
            AddToFilterDictionary(filterProperties: UWF_Volume, _WMI_PropertyName_Protected);
            AddToFilterDictionary(filterProperties: UWF_Volume, _WMI_PropertyName_VolumeName);
            // Error flags
            AddToFilterDictionary(filterProperties: UWF_Volume, _WMIQueryError_NativeErrorCode);
            AddToFilterDictionary(filterProperties: UWF_Volume, _WMIQueryError_HResult);
            AddToFilterDictionary(filterProperties: UWF_Volume, _WMIQueryError_Message);
            AddToFilterDictionary(filterProperties: UWF_Volume, _WMIQueryError_ErrorOccurred);
            AddToFilterDictionary(filterProperties: UWF_Volume, _WMIFailedConnectionTest);

            UWF_Servicing = new Dictionary<string, FilterProperty>();
            AddToFilterDictionary(filterProperties: UWF_Servicing, _WMI_PropertyName_CurrentSession);
            AddToFilterDictionary(filterProperties: UWF_Servicing, _WMI_PropertyName_ServicingEnabled);
            // Error flags
            AddToFilterDictionary(filterProperties: UWF_Servicing, _WMIQueryError_NativeErrorCode);
            AddToFilterDictionary(filterProperties: UWF_Servicing, _WMIQueryError_HResult);
            AddToFilterDictionary(filterProperties: UWF_Servicing, _WMIQueryError_Message);
            AddToFilterDictionary(filterProperties: UWF_Servicing, _WMIQueryError_ErrorOccurred);
            AddToFilterDictionary(filterProperties: UWF_Servicing, _WMIFailedConnectionTest);

            UWF_Servicing2 = new Dictionary<string, FilterProperty>();
            AddToFilterDictionary(filterProperties: UWF_Servicing2, _WMI_PropertyName_CurrentSession);
            AddToFilterDictionary(filterProperties: UWF_Servicing2, _WMI_PropertyName_ServicingEnabled);
            // Error flags
            AddToFilterDictionary(filterProperties: UWF_Servicing2, _WMIQueryError_NativeErrorCode);
            AddToFilterDictionary(filterProperties: UWF_Servicing2, _WMIQueryError_HResult);
            AddToFilterDictionary(filterProperties: UWF_Servicing2, _WMIQueryError_Message);
            AddToFilterDictionary(filterProperties: UWF_Servicing2, _WMIQueryError_ErrorOccurred);
            AddToFilterDictionary(filterProperties: UWF_Servicing2, _WMIFailedConnectionTest);
        }

        /// <summary>
        /// Re-Initialises all required filter dictionaries.
        /// This means that they are first cleared and then reinitialized with standard value pairs.
        /// 
        /// Use this function before using an already initialized UWF Class instance to repeatedly determine UWF properties!
        /// </summary>
        public void ReInitFilterDictionaries()
        {
            // Clear all uwf class dictionaries.
            UWF_Filters.Clear();
            UWF_Overlay.Clear();
            UWF_OverlayConfig.Clear();
            UWF_OverlayConfig2.Clear();
            UWF_Servicing.Clear();
            UWF_Servicing2.Clear();
            UWF_Volume.Clear();

            // Initialize all uwf class dictionaries.
            InitFilterDictionaries();
        }

        /// <summary>
        /// Initialises the filter dictionary with key names and empty property values (add key/value pairs)
        /// for use as data containers for WMI queries from the UWF environment.
        /// See also under:
        /// - the class constructor,
        /// - the class FilterProperty,
        /// - the class global defined dictionary,
        /// - GetWmiClassProperties().
        /// *** Example ***
        /// To determine the WMI class properties: "CurrentEnabled", "NextEnabled" of the class UWF_Filters (see also function: GetWmiClassProperties):
        /// AddToFilterDictionary(filterProperties: UWF_Filters, "CurrentEnabled");
        /// AddToFilterDictionary(filterProperties: UWF_Filters, "NextEnabled");
        /// </summary>
        /// <param name="filterProperties">A dictionary instance whose values are to be initialised.</param>
        /// <param name="keyName">Name of the key to be set.The name must correspond to the name of the WMI property searched/needed.</param>
        private void AddToFilterDictionary(Dictionary<string, FilterProperty> filterProperties, string keyName)
        {
            FilterProperty filterProperty = new FilterProperty(String.Empty);           
            filterProperties.Add(key: keyName, value: filterProperty);
        }        
        #endregion

        #region WMI_Queries: UWF_Filter, UWF_Overlay, UWF_OverlayConfig (UWF_OverlayConfigNext), UWF_Volume, UWF_Servicing, Win32_OperationalFeature.
        const string _QUERY_LANGUAGE = "WQL";
        const string _DEFAULT_WMI_NAMESPACE = @"ROOT\StandardCimv2\embedded";
        const string _ROOT_CIMV2_NAMESPACE = @"ROOT\Cimv2";
        const string _UWF_CLASS = "UWF_Filter";
        const string _WMI_Query_UWF_FilterStatus = "SELECT * FROM UWF_Filter";
        const string _WMI_Query_UWF_Overlay = "SELECT * FROM UWF_Overlay";
        const string _WMI_Query_UWF_OverlayConfig = "SELECT * FROM UWF_OverlayConfig";
        const string _WMI_Query_UWF_Volume = "SELECT * FROM UWF_Volume";
        const string _WMI_Query_UWF_Servicing = "SELECT * FROM UWF_Servicing";
        const string _WMI_Query_WIN32_OperationalFeature_UWF = "SELECT * FROM Win32_OptionalFeature WHERE Name='Client-DeviceLockdown'";

        public string WMI_Query_UWF_FilterStatus => _WMI_Query_UWF_FilterStatus;
        public string WMI_Query_UWF_Overlay => _WMI_Query_UWF_Overlay;
        public string WMI_Query_UWF_OverlayConfig => _WMI_Query_UWF_OverlayConfig;
        public string WMI_Query_UWF_Volume => _WMI_Query_UWF_Volume;
        public string WMI_Query_UWF_Servicing => _WMI_Query_UWF_Servicing;
        public string WMI_NAMESPACE_ROOT_CIMV2 => _ROOT_CIMV2_NAMESPACE;
        public string WMI_Query_WIN32_OperationalFeature_UWF => _WMI_Query_WIN32_OperationalFeature_UWF;
        #endregion

        #region WMI_Properties: Query errors and other user-defined values like the native error codes or failed connection tests.
        const string _WMIQueryError_ErrorOccurred = "WMI_Err_ErrorOccurred";
        const string _WMIQueryError_NativeErrorCode = "WMI_Err_NativeErrorCode";
        const string _WMIQueryError_HResult = "WMI_Err_HResult";
        const string _WMIQueryError_Message = "WMI_Err_Message";
        const string _WMIFailedConnectionTest = "WMI_Err_TestConnectionFailed";

        public string WMIQueryError_ErrorOccurred => _WMIQueryError_ErrorOccurred;
        public string WMIQueryError_NativeErrorCode => _WMIQueryError_NativeErrorCode;
        public string WMIQueryError_HResult => _WMIQueryError_HResult;
        public string WMIQueryError_Message => _WMIQueryError_Message;
        public string WMIFailedConnectionTest => _WMIFailedConnectionTest;
        #endregion

        #region WMI_Properties: Common UWF properties
        const string _WMI_PropertyName_Id = "Id";
        const string _WMI_PropertyName_CurrentSession = "CurrentSession";
        public string WMI_PropertyName_Id => _WMI_PropertyName_Id;
        public string WMI_PropertyName_CurrentSession => _WMI_PropertyName_CurrentSession;
        #endregion

        #region WMI_Properties: UWF_Filter
        const string _WMI_PropertyName_CurrentEnabled = "CurrentEnabled";
        const string _WMI_PropertyName_NextEnabled = "NextEnabled";
        const string _WMI_PropertyName_HORMEnabled = "HORMEnabled";
        const string _WMI_ProopertyName_ShutdownPending = "ShutdownPending";
        public string WMI_PropertyName_CurrentEnabled => _WMI_PropertyName_CurrentEnabled;
        public string WMI_PropertyName_NextEnabled => _WMI_PropertyName_NextEnabled;
        public string WMI_PropertyName_HORMEnabled => _WMI_PropertyName_HORMEnabled;
        public string WMI_PropertyName_ShutdownPending => _WMI_ProopertyName_ShutdownPending;
        #endregion

        #region WMI_Properties: UWF_Overlay
        const string _WMI_PropertyName_AvailableSpace = "AvailableSpace";
        const string _WMI_PropertyName_CriticalOverlayTreshold = "CriticalOverlayThreshold";
        const string _WMI_PropertyName_OverlayConsumption = "OverlayConsumption";
        const string _WMI_PropertyName_WarningOverlayThreshold = "WarningOverlayThreshold";
        public string WMI_PropertyName_AvailableSpace => _WMI_PropertyName_AvailableSpace;
        public string WMI_PropertyName_CriticalOverlayTreshold => _WMI_PropertyName_CriticalOverlayTreshold;
        public string WMI_PropertyName_OverlayConsumption => _WMI_PropertyName_OverlayConsumption;
        public string WMI_PropertyName_WarningOverlayThreshold => _WMI_PropertyName_WarningOverlayThreshold;
        #endregion

        #region WMI_Properties: UWF_OverlayConfig
        const string _WMI_PropertyName_MaximumSize = "MaximumSize";
        const string _WMI_PropertyName_Type = "Type";
        public string WMI_PropertyName_MaximumSize => _WMI_PropertyName_MaximumSize;
        public string WMI_PropertyName_Type => _WMI_PropertyName_Type;
        #endregion

        #region WMI_Properties: UWF_Volume
        const string _WMI_PropertyName_BindByDriveLetter = "BindByDriveLetter";
        const string _WMI_PropertyName_CommitPending = "CommitPending";
        const string _WMI_PropertyName_DriveLetter = "DriveLetter";
        const string _WMI_PropertyName_Protected = "Protected";
        const string _WMI_PropertyName_VolumeName = "VolumeName";
        public string WMI_PropertyName_BindByDriveLetter => _WMI_PropertyName_BindByDriveLetter;
        public string WMI_PropertyName_CommitPending => _WMI_PropertyName_CommitPending;
        public string WMI_PropertyName_DriveLetter => _WMI_PropertyName_DriveLetter;
        public string WMI_PropertyName_Protected => _WMI_PropertyName_Protected;
        public string WMI_PropertyName_VolumeName => _WMI_PropertyName_VolumeName;
        #endregion

        #region WMI_Properties: UWF_Servicing
        const string _WMI_PropertyName_ServicingEnabled = "ServicingEnabled";
        public string WMI_PropertyName_ServicingEnabled => _WMI_PropertyName_ServicingEnabled;
        #endregion

        #region Methods for accessing WMI

        /// <summary>
        /// Gets all property values of a WMI class and returns them in a dictionary.
        /// </summary>
        /// <param name="wmiQuery">WQL string for accessing the respective UWF class, e.g. "Select * from UWF_Volume".</param>
        /// <param name="propDictionary">Reference to the FilterProperty dictionary. This is where the collected data is stored.</param>
        /// <param name="token">CancellationToken to cancel the job.</param>
        /// <param name="computer">Computer (DNS or NetBIOS) name whose UWF properties are to be read.</param>
        /// <param name="instanceLevel">Which instance level should be determined, e.g. 1.</param>
        /// <returns>ErrorCode: different states.</returns>
        public ErrorCode GetWmiClassProperties(string wmiQuery, ref Dictionary<string, FilterProperty> propDictionary, CancellationToken token, string computer = "", int instanceLevel = 0)
        {
            // Initialize the method return code.
            ErrorCode errorCode = ErrorCode.InitState;

            // Initialize the WMI session.
            CimSession currentCimSession = null;

            // Set the wmi query error flag to "false";
            propDictionary[_WMIQueryError_ErrorOccurred].SetPropertyValue("false");

            try
            {
                // If no computer name is passed, we must use a null in the WMI query.
                if (String.IsNullOrEmpty(computer)) computer = null;

                // Set the CIM session time out to 2 minutes.
                CimSessionOptions cimSessionOptions = new DComSessionOptions
                {
                    Timeout = new TimeSpan(0, 2, 0)
                };

                // Create a session to a local or remote computer.
                currentCimSession = CimSession.Create(computer, cimSessionOptions);

                // Test the connection.              
                if (currentCimSession.TestConnection() == false)
                {
                    // The connection failed, so we don't have wmi valid data.
                    propDictionary[_WMIFailedConnectionTest].SetPropertyValue("true");
                    return ErrorCode.TestConnectionFailed | ErrorCode.NoDataAvailable;
                }
                else
                {
                    // Testing of the WMI connection was successful. 
                    propDictionary[_WMIFailedConnectionTest].SetPropertyValue("false");
                }
                
                // Query instances of the WMI class.
                IEnumerable <CimInstance> queryInstances = currentCimSession.QueryInstances(_DEFAULT_WMI_NAMESPACE, _QUERY_LANGUAGE, wmiQuery);

                // Used to monitor the instance level.
                int currentInstanceLevel = 0;

                // Iterate through all available instances and search for the desired property.
                foreach (CimInstance cimInstance in queryInstances)
                {                                       
                    // Iterate the desired instance.
                    if (instanceLevel == currentInstanceLevel)
                    {
                        // Go throu all cim instance properties.
                        foreach (var cimProperty in cimInstance.CimInstanceProperties)
                        {
                            // Cancel task because user triggered the cancellation.
                            Thread.Sleep(100);
                            token.ThrowIfCancellationRequested();

                            // Check if the property name matches the name searched for (in the dictionary).
                            if (propDictionary.ContainsKey(cimProperty.Name) == true)
                            {
                                // Check if the cimProperty is valid (not null).
                                if (cimProperty.Value != null)
                                {
                                    propDictionary[cimProperty.Name].SetPropertyValue(cimProperty.Value.ToString());
                                }
                                else
                                {
                                    propDictionary[cimProperty.Name].SetPropertyValue(string.Empty);
                                }
                            }
                        }
                        // Set the return code properly to signal valid data.
                        errorCode = ErrorCode.DataAvailable;

                        // Cancel the loop after the desired instanceLevel.
                        break;
                    }
                    currentInstanceLevel++;
                }
            }
            catch (OperationCanceledException e)
            {
                // return code
                errorCode |= ErrorCode.OperationCanceled;
                               
                // Log the error information in the affected dictionary.
                LogWmiExceptionToDict(e, ref propDictionary);

                // Preserve the stack trace and throw the exeption (plain re-throw).
                throw;
            }
            catch (CimException e)
            {
                // return codes (class and method)
                errorCode |= ErrorCode.ErrorOccured | ErrorCode.ExceptionTypeCim;

                // Log the error information in the affected dictionary.
                LogWmiExceptionToDict(e, ref propDictionary);

                // Preserve the stack trace and throw the exeption (plain re-throw).
                throw;
            }
            catch (Exception e)
            {
                // return codes (class and method)
                errorCode |= ErrorCode.ErrorOccured | ErrorCode.ExceptionTypeStandard;

                // Log the error information in the affected dictionary.
                LogWmiExceptionToDict(e, ref propDictionary);

                // Preserve the stack trace and throw the exeption (plain re-throw).
                throw;
            }
            finally
            {
                // Save the last error code into a class member.
                LastError = errorCode;

                // Dispose the object.
                if (currentCimSession != null)
                {
                    currentCimSession.Dispose();
                }
            }
            return errorCode;
        }

        /// <summary>
        /// Provides information from all available instances of the UWF_Volume WMI class.
        /// This can be used to determine which drives are protected, for example.
        /// Attention: To determine the properties of the UWF_Volume WMI class, elevated rights (administrator) are required.
        /// 
        /// Returns a list of all available UWF_Volume instances and the properties they contain.
        /// Possible return values are: Null (on error), an empty list (0-Count) even on error or a filled list (>0-Count).  
        /// </summary>
        /// <param name="computer"></param>
        /// <returns>List<ProtectedVolume></returns>
        public List<ProtectedVolume> GetProtectedVolumes(string computer)
        {
            // List with protected volumes.
            List<ProtectedVolume> listProtectedVolumes = null;

            // Container with detailed UWF_Volume data.
            ProtectedVolume protectedVolume = null;
            
            // Initialize the WMI session.
            CimSession currentCimSession = null;

            try
            {
                // If no computer name is passed, we must use a null in the WMI query.
                if (String.IsNullOrEmpty(computer)) computer = null;

                // Set the CIM session time out to 2 minutes.
                CimSessionOptions cimSessionOptions = new DComSessionOptions
                {
                    Timeout = new TimeSpan(0, 2, 0)
                };

                // Create a session to a local or remote computer.
                currentCimSession = CimSession.Create(computer, cimSessionOptions);

                // Test the connection.
                if (currentCimSession.TestConnection() == false)
                {
                    // The connection failed, so we don't have valid UWF_Volume data and return a null;
                    return null;
                }

                listProtectedVolumes = new List<ProtectedVolume>();

                // Query instances of the WMI class.
                IEnumerable<CimInstance> queryInstances = currentCimSession.QueryInstances(_DEFAULT_WMI_NAMESPACE, _QUERY_LANGUAGE, _WMI_Query_UWF_Volume);

                // Iterate through all available instances and collect the properties.
                foreach (CimInstance cimInstance in queryInstances)
                {
                    // Create a data container instance.
                    protectedVolume = new ProtectedVolume();

                    // Go through all cim instance properties.
                    foreach (var cimProperty in cimInstance.CimInstanceProperties)
                    {
                        switch(cimProperty.Name.ToLower())
                        {
                            case "currentsession":
                                if (cimProperty.Value != null) protectedVolume.currentSession = (bool)cimProperty.Value;
                                break;
                            case "protected":
                                if (cimProperty.Value != null) protectedVolume.isProtected = (bool)cimProperty.Value;
                                break;
                            case "bindbydriveletter":
                                if (cimProperty.Value != null) protectedVolume.bindByDriveLetter = (bool)cimProperty.Value;
                                break;
                            case "commitpending":
                                if (cimProperty.Value != null) protectedVolume.commitPending = (bool)cimProperty.Value;
                                break;
                            case "volumename":
                                if (cimProperty.Value != null) protectedVolume.volumeName = cimProperty.Value.ToString();
                                break;
                            case "driveletter":
                                if (cimProperty.Value != null) protectedVolume.driveLetter = cimProperty.Value.ToString();
                                break;
                        }                        
                    }
                    // Add the container class filled with data from the UWF_Volume to the return list.
                    listProtectedVolumes.Add(protectedVolume);
                }
            }
            catch (CimException)
            {
                // return codes (class and method)
                lastError |= ErrorCode.ErrorOccured | ErrorCode.ExceptionTypeCim;

                // Preserve the stack trace and throw the exeption (plain re-throw).
                throw;
            }
            catch (Exception)
            {
                // return codes (class and method)
                lastError |= ErrorCode.ErrorOccured | ErrorCode.ExceptionTypeStandard;

                // Preserve the stack trace and throw the exeption (plain re-throw).
                throw;
            }
            finally
            {
                // Dispose the object.
                if (currentCimSession != null)
                {
                    currentCimSession.Dispose();
                } 
            }
            // The following values can be returned: Null list, a list with 0 or > 0 count.
            return listProtectedVolumes;
        }

        /// <summary>
        /// Calls an asynchronous and parameterless method from the UWF_Filter class.
        /// </summary>
        /// <param name="computer">Name of the local or remote computer (DNS or NetBIOS) on which the desired method (methodName) should be executed.</param>
        /// <param name="methodName">Name of the parameterless method to be executed.
        /// The defined method names can be found in the UWFFilterMethodName struct.</param>
        /// <returns></returns>
        public ErrorCode InvokeFilterMethodAsynch(string methodName, string computer = "")
        {
            // Initialize the method return code.
            ErrorCode errorCode = ErrorCode.InitState;

            // Initialize the WMI session.
            CimSession cimSession = null;

            // Observer
            CimObserver<CimMethodResultBase> observer = null;

            try
            {
                // If no computer name is passed, we must use a null in the WMI query.
                if (String.IsNullOrEmpty(computer)) computer = null;

                // Set the CIM session time out to 2 minutes.
                CimSessionOptions cimSessionOptions = new DComSessionOptions { Timeout = new TimeSpan(0, 2, 0) };

                // Set the CIM operation options.
                CimOperationOptions cimOperationOptions = new CimOperationOptions { Timeout = new TimeSpan(0, 1, 0) };
            
                // Create a session to a local or remote computer.
                cimSession = CimSession.Create(computer, cimSessionOptions);

                // Method parameters: The both functions Enable and Disable have only out parameters.
                // For these we do not need to define any parameters like with
                // methodParameters.CimInstanceProperties.Add(CimProperty.Create("IgnoreNonSnapshottableDisks", true, CimFlags.ReadOnly)).
                CimMethodParametersCollection methodParameters = new CimMethodParametersCollection();

                // Get the instance of UWF_Filter, there is only one.
                var cimInstance = cimSession.EnumerateInstances(_DEFAULT_WMI_NAMESPACE, _UWF_CLASS).First();

                // Run asynch method.
                // The InvokeMethodAsync call must be made using the instance parameter and not using the cim class name.
                CimAsyncMultipleResults<CimMethodResultBase> invokeParams = cimSession.InvokeMethodAsync(_DEFAULT_WMI_NAMESPACE,
                                                                                                         cimInstance,
                                                                                                         methodName,
                                                                                                         methodParameters, cimOperationOptions);

                // Create an observer to watch the invoke of the InvokeMethodAsynch.
                observer = new CimObserver<CimMethodResultBase>();
                IDisposable disposable = invokeParams.Subscribe(observer);
                observer.WaitForCompletion();
                
            }
            catch (CimException e)
            {
                errorCode |= ErrorCode.ErrorOccured | ErrorCode.ExceptionTypeStandard;
                Console.Write("EnableFilterAsynch (CimException): {0}", e.Message);
            }
            catch (Exception e)
            {
                Console.Write("EnableFilterAsynch (Exception): {0}", e.Message);
            }
            finally
            {
                // Dispose the object.
                if (observer != null) observer.Dispose();
                if (cimSession != null) cimSession.Dispose();
            }
            return errorCode;
        }

        /// <summary>
        /// Retrieves the value of a property of a WMI class (UI thread-safe function).
        /// </summary>
        /// <param name="wmiQuery">WQL string with the WMI query.</param>
        /// <param name="wmiProperty">Name of the WMI property to be obtained.</param>
        /// <param name="outPropertyValue">Output buffer for the obtained property value.</param>
        /// <param name="computer">Specifies the computer name whose WMI property is to be obtained.</param>
        /// <param name="nameSpace">Class namespace containing the WMI class used.</param>
        /// <returns>Returns either 0x0 (no data available) or 0x1 (data available) on success. If an exception occurred while processing, returns 0x2.</returns>
        public ErrorCode GetWmiClassProperty(string wmiQuery, string wmiProperty, ref string outPropertyValue, string computer = "", string nameSpace = _DEFAULT_WMI_NAMESPACE)
        {
            // Returned property value.
            string tmpPropertyValue = "";
            ErrorCode errorCode = ErrorCode.InitState;

            try
            {
                // If no computer name is passed, we must use a null in the WMI query.
                if (String.IsNullOrEmpty(computer)) computer = null;

                // Set the CIM session time out to 2 minutes.
                CimSessionOptions cimSessionOptions = new DComSessionOptions
                {
                    Timeout = new TimeSpan(0, 2, 0)
                };

                // Create a session to a local or remote computer.
                CimSession currentCimSession = CimSession.Create(computer, cimSessionOptions);

                IEnumerable<CimInstance> queryInstance = currentCimSession.QueryInstances(nameSpace, _QUERY_LANGUAGE, wmiQuery);

                // Iterate through all available instances and search for the desired property.
                foreach (CimInstance cimInstance in queryInstance)
                {
                    if (cimInstance.CimInstanceProperties[wmiProperty] != null)
                    {
                        string v = cimInstance.CimInstanceProperties[wmiProperty].Value.ToString();
                        tmpPropertyValue = v;
                        errorCode = ErrorCode.DataAvailable;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // return codes (class and method)
                lastError |= ErrorCode.ErrorOccured | ErrorCode.ExceptionTypeStandard;

                tmpPropertyValue = ex.Message.ToString();
                errorCode =  ErrorCode.ErrorOccured;
            }
            finally
            {
                outPropertyValue = tmpPropertyValue;               
            }
            return errorCode;
        }

        #endregion

        #region State and error handling
        
        #region Return codes structure
        /// <summary>
        /// Enum with return codes of functions / methods.
        /// </summary>
        [Flags]
        public enum ErrorCode : short
        {
            InitState = 0x0,
            ErrorOccured = 0x1,
            DataAvailable = 0x2,
            NoDataAvailable = 0x4,
            TestConnectionFailed = 0x8,
            OperationCanceled = 0x10,
            reserved = 0x20,
            ExceptionTypeCim = 0x40,
            ExceptionTypeStandard = 0x80
        }
        #endregion

        /// <summary>
        /// Class member variable containing the last error that occurred.
        /// </summary>
        private ErrorCode lastError;
        public ErrorCode LastError
        {
            get => lastError;
            set => lastError =value;
        }       

        /// <summary>
        /// Overloaded function for logging standard exceptions in a propDictionary. 
        /// </summary>
        /// <param name="e">Exception object</param>
        /// <param name="propDictionary">Reference to the instantiated dictionary during whose processing the error occurred.</param>
        private void LogWmiExceptionToDict(Exception e, ref Dictionary<string, FilterProperty> propDictionary)
        {
            propDictionary[_WMIQueryError_ErrorOccurred].SetPropertyValue("true");
            propDictionary[_WMIQueryError_NativeErrorCode].SetPropertyValue("NoNativeErrorCode");
            propDictionary[_WMIQueryError_HResult].SetPropertyValue(e.HResult.ToString());
            propDictionary[_WMIQueryError_Message].SetPropertyValue(e.Message);
        }
        /// <summary>
        /// Overloaded function for logging CIM exceptions in a propDictionary.
        /// </summary>
        /// <param name="e">CIM Exception object</param>
        /// <param name="propDictionary">Reference to the instantiated dictionary during whose processing the error occurred.</param>
        private void LogWmiExceptionToDict(CimException e, ref Dictionary<string, FilterProperty> propDictionary)
        {
            propDictionary[_WMIQueryError_ErrorOccurred].SetPropertyValue("true");
            propDictionary[_WMIQueryError_NativeErrorCode].SetPropertyValue(e.NativeErrorCode.ToString());
            propDictionary[_WMIQueryError_HResult].SetPropertyValue(e.HResult.ToString());
            propDictionary[_WMIQueryError_Message].SetPropertyValue(e.Message);
        }

        #endregion
    }
       
    public class CimObserver<T> : IObserver<T>, IDisposable
    {     
        private readonly ManualResetEventSlim doneEvent = new ManualResetEventSlim(false);

        public void OnNext(T value)
        {
            CimInstance instance = value as CimInstance;
            if (instance != null)
            {
                Console.WriteLine("Value " + instance);
                return;
            }

            CimMethodResult methodResult = value as CimMethodResult;
            if (methodResult != null)
            {
                Console.WriteLine("Value " + methodResult);
                return;
            }

            CimMethodStreamedResult methodStreamResult = value as CimMethodStreamedResult;
            if (methodStreamResult != null)
            {
                Console.WriteLine("Value " + methodStreamResult);
            }

            CimSubscriptionResult subscriptionResult = value as CimSubscriptionResult;
            if (subscriptionResult != null)
            {
                Console.WriteLine("Value " + subscriptionResult.Instance);
                return;
            }
        }

        public void OnError(Exception e)
        {
            CimException cimException = e as CimException;
            if (cimException != null)
            {
                Console.WriteLine("Value " + cimException.Message);
            }
            else
            {
                throw e;
            }

            this.doneEvent.Set();
        }

        public void OnCompleted()
        {
            this.doneEvent.Set();
        }

        public void WaitForCompletion()
        {
            this.doneEvent.Wait();
        }

        #region IDisposable Members

        /// <summary>
        /// Releases resources associated with this object.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources associated with this object.
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                doneEvent.Dispose();
            }

            disposed = true;
        }

        private bool disposed = true;
        #endregion IDisposable Members
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Instance variables
        UnifiedWriteFilter uwf;
        CancellationTokenSource tokenSource;
        string computer;
        readonly CultureInfo cultureInfo;
        readonly ResourceManager resourceManager;
        bool isAdmin = false;
        bool isUWFInstalled = false;
        const int MAX_SEARCHT_ITEMS = 15;
        #endregion

        #region Bindings
        private sealed class DataViewModel : INotifyPropertyChanged
        {
            // Interface member.
            public event PropertyChangedEventHandler PropertyChanged;

            private string lastFilterAction;
            public string LastFilterAction
            {
                get { return lastFilterAction; }
                set 
                { 
                    lastFilterAction = value;
                    var handler = PropertyChanged;
                    if (handler != null)
                    {
                        handler(this, new PropertyChangedEventArgs("LastFilterAction"));
                    }
                }
            }

            private string computer;
            public string Computer
            {
                get { return computer; }
                set
                {
                    computer = value;
                    var handler = PropertyChanged;
                    if (handler != null)
                    {
                        handler(this, new PropertyChangedEventArgs("Computer"));
                    }
                }
            }

            private bool restartComputer;
            public bool RestartComputer
            {
                get { return restartComputer; }
                set
                {
                    restartComputer = value;
                    var handler = PropertyChanged;
                    if (handler != null)
                    {
                        handler(this, new PropertyChangedEventArgs("RestartComputer"));
                    }
                }
            }

            private bool userInformed;
            public bool UserInformed
            {
                get { return userInformed; }
                set
                {
                    userInformed = value;
                    var handler = PropertyChanged;
                    if (handler != null)
                    {
                        handler(this, new PropertyChangedEventArgs("UserInformed"));
                    }
                }
            }
        }    
        #endregion

        /// <summary>
        /// The central entry function of this application.
        /// </summary>
        public MainWindow()
        {
            /**
             * Determine the system language to use as the UI language (National Language Support).
             * See also under:  https://msdn.microsoft.com/en-us/goglobal/bb896001.aspx
             * All supported languages are managed via the corresponding resources file, e.g. for the german langauge (de-DE) Resources.de-DE.resx
             * For english see under Resources.resx.
             */
cultureInfo = CultureInfo.InstalledUICulture;

            // Testing a different language (culture) than the one installed.
#if DEBUG
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("de-DE");
            cultureInfo = System.Threading.Thread.CurrentThread.CurrentUICulture;
#endif
            // Create the CultureInfo class instance to identify linguistically appropriate terms on the respective language file (ressources.*.resx).
            // All strings come from the resources file in the respective language.
            // cultureInfo = CultureInfo.CreateSpecificCulture(cultureInfo.Name);
            resourceManager = new ResourceManager(typeof(Cregx.Uwf.Dashboard.Properties.Resources));

#if DEBUG
            Console.WriteLine("System language: {0}", cultureInfo.Name);
            Console.WriteLine("System display language name: {0}", cultureInfo.DisplayName);
#endif

            // Initialize components.
            InitializeComponent();

            // Register events for the window menus: minimize and close.
            this.MinimizeButton.Click += (s, e) => WindowState = WindowState.Minimized;
            this.CloseButton.Click += (s, e) => Close();

            #region --- Assembly informations ---

            //--- Assembly-Infos ---
            var attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
            string company = "";
            if (attributes.Length > 0)
            {
                company = ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
            
            OutputAppInfo.Content = String.Format("App Ver. {0} ({1}) / {2} {3}",  Assembly.GetExecutingAssembly().GetName().Version.ToString(), "Dev-Build", "MIT License Copyright (c) 2020-2021", company);
            OutputVisitProjectSite.Text = String.Format(resourceManager.GetString("Visit_Project_Site", cultureInfo), "https://www.cregx.de/docs/uwfdashboard/");

            #endregion

            #region Databinding
            DataContext = new DataViewModel();
            #endregion
        }

        /// <summary>
        /// Event: It is fired when the main UI window is loaded.
        /// See also: MainWindow.xaml
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WndMainWindow_Loaded(object sender, RoutedEventArgs e)
        { 
            /** 
             * Instantiate the main class of the application.
             * An instance of this class contains all the information needed to access the UWF environment.
             * */
            uwf = new UnifiedWriteFilter();

            // Check that the application is running with administrative rights. (If not, display this info in the UI).
            isAdmin = Helpers.IsAdministrator();
            OutputImportantNotes.Content = (isAdmin == true) ? resourceManager.GetString("Is_Administrator", cultureInfo) : resourceManager.GetString("Is_Not_Administrator");
            OutputImportantNotesIcon.Kind = (isAdmin == true) ? MaterialDesignThemes.Wpf.PackIconKind.ShieldAlert : MaterialDesignThemes.Wpf.PackIconKind.Account;
            
            // Reset output controls. 
            CleanUI();
        }

        /// <summary>
        /// Clears the output elements in the UI.
        /// </summary>
        private void CleanUI()
        {
            const string uiDefaultValue = "-";

            // Current session data.
            OutputCriticalTreshold.Content = uiDefaultValue;
            OutputFilterStatus.Content = uiDefaultValue;
            OutputHORMEnabled.Content = uiDefaultValue;
            OutputMaximumSize.Content = uiDefaultValue;
            OutputNextFilterStatus.Content = uiDefaultValue;
            OutputProtectedVolume.Content = uiDefaultValue;
            OutputServicingEnabled.Content = uiDefaultValue;
            OutputShutdownPending.Content = uiDefaultValue;
            OutputVolumeType.Content = uiDefaultValue;
            OutputWarningTreshold.Content = uiDefaultValue;

            // Next session data.
            OutputNextCriticalTreshold.Content = uiDefaultValue;
            OutputNextFilterStatus.Content = uiDefaultValue;   
            OutputNextMaximumSize.Content = uiDefaultValue;
            OutputNextFilterStatus.Content = uiDefaultValue;
            OutputNextProtectedVolume.Content = uiDefaultValue;
            OutputNextServicingEnabled.Content = uiDefaultValue;
            OutputNextVolumeType.Content = uiDefaultValue;
            OutputNextWarningTreshold.Content = uiDefaultValue;

            // Status bar.
            SetStatusBarText(cultureInfo, resourceManager, "Operation_Ready", String.Empty, false);
        }

        #region UI output collected data
        /// <summary>
        /// Outputs asynch collected information from all uwf dictionaries in the UI.
        /// </summary>
        /// <returns>True if the operation has failed, otherwise false.</returns>
        private async Task<bool> UIOutput_UWF_Status()
        {
            bool hasFailed = false;

            #region NLS, e.g. values for the terms "On" and "Off".
            // For the CultureInfo see under the class constructor: Instance to identify linguistically
            // appropriate terms on the respective language file (ressources.*.resx).
            string onTerm = resourceManager.GetString("UWF_ON", cultureInfo);
            string offTerm = resourceManager.GetString("UWF_OFF", cultureInfo);
            string uwfType_0 = resourceManager.GetString("UWF_Type_0", cultureInfo);
            string uwfType_1 = resourceManager.GetString("UWF_Type_1", cultureInfo);
            #endregion

            #region UWF_Filter (Current und NextSession)
            try
            {
                OutputFilterStatus.Content = Convert.ToBoolean(uwf.UWF_Filters[uwf.WMI_PropertyName_CurrentEnabled].GetPropertyValue()) ? onTerm : offTerm;
                OutputNextFilterStatus.Content = Convert.ToBoolean(uwf.UWF_Filters[uwf.WMI_PropertyName_NextEnabled].GetPropertyValue()) ? onTerm : offTerm;
                OutputShutdownPending.Content = Convert.ToBoolean(uwf.UWF_Filters[uwf.WMI_PropertyName_ShutdownPending].GetPropertyValue()) ? onTerm : offTerm;
                OutputHORMEnabled.Content = Convert.ToBoolean(uwf.UWF_Filters[uwf.WMI_PropertyName_HORMEnabled].GetPropertyValue()) ? onTerm : offTerm;
            }
            catch (Exception ex)
            {
                SetStatusBarText(cultureInfo, resourceManager, "Exception_Occurred", ex.HResult.ToString(), true);
                hasFailed = true;
            }
            #endregion

            #region UWF_Overlay (CurrentSession and NextSession)
            // Current session
            OutputWarningTreshold.Content = String.Format("{0} MB", uwf.UWF_Overlay[uwf.WMI_PropertyName_WarningOverlayThreshold].GetPropertyValue());
            OutputCriticalTreshold.Content = String.Format("{0} MB", uwf.UWF_Overlay[uwf.WMI_PropertyName_CriticalOverlayTreshold].GetPropertyValue());

            // Next session
            OutputNextWarningTreshold.Content = String.Format("{0} MB", uwf.UWF_Overlay[uwf.WMI_PropertyName_WarningOverlayThreshold].GetPropertyValue());
            OutputNextCriticalTreshold.Content = String.Format("{0} MB", uwf.UWF_Overlay[uwf.WMI_PropertyName_CriticalOverlayTreshold].GetPropertyValue());
            #endregion

            #region UWF_OverlayConfig (CurrentSession, it should be the instanceLevel 0)
            // Check that this is the CurrentSession.
            if (uwf.UWF_OverlayConfig[uwf.WMI_PropertyName_CurrentSession].GetPropertyValue().ToLower() == "true")
            {
                // Type: RAM == 0 | SSD == 1
                if (Int32.TryParse(uwf.UWF_OverlayConfig[uwf.WMI_PropertyName_Type].GetPropertyValue().ToString(), out int parsedValue))
                {
                    OutputVolumeType.Content = (parsedValue == 1) ? uwfType_1 : uwfType_0;
                }
                // MaximumSize
                OutputMaximumSize.Content = String.Format("{0} MB", uwf.UWF_OverlayConfig[uwf.WMI_PropertyName_MaximumSize].GetPropertyValue());
            }
            #endregion

            #region UWF_OverlayConfig2 (NextSession, it should be the instanceLevel 1)
            // Check that this is the NextSession.
            if (uwf.UWF_OverlayConfig2[uwf.WMI_PropertyName_CurrentSession].GetPropertyValue().ToLower() == "false")
            {
                // Type: RAM == 0 | SSD == 1
                if (Int32.TryParse(uwf.UWF_OverlayConfig2[uwf.WMI_PropertyName_Type].GetPropertyValue().ToString(), out int parsedValue))
                {
                    OutputNextVolumeType.Content = (parsedValue == 1) ? uwfType_1 : uwfType_0;
                }
                // MaximumSize
                OutputNextMaximumSize.Content = String.Format("{0} MB", uwf.UWF_OverlayConfig2[uwf.WMI_PropertyName_MaximumSize].GetPropertyValue());
            }
            #endregion

            #region UWF_Servicing (CurrentSession, it should be the instanceLevel 0)
            // Check that this is the CurrentSession.
            if (uwf.UWF_Servicing[uwf.WMI_PropertyName_CurrentSession].GetPropertyValue().ToLower() == "true")
            {
                try
                {
                    // ServicingEnabled
                    OutputServicingEnabled.Content = Convert.ToBoolean(uwf.UWF_Servicing[uwf.WMI_PropertyName_ServicingEnabled].GetPropertyValue()) ? onTerm : offTerm;
                }
                catch (Exception ex)
                {
                    SetStatusBarText(cultureInfo, resourceManager, "Exception_Occurred", ex.HResult.ToString(), true);
                    hasFailed = true;
                }
            }
            #endregion

            #region UWF_Servicing2 (NextSession, it should be the instanceLevel 1)
            // Check that this is the NextSession.
            if (uwf.UWF_Servicing2[uwf.WMI_PropertyName_CurrentSession].GetPropertyValue().ToLower() == "false")
            {
                try
                {
                    // Check that this is the NEXT session.
                    OutputNextServicingEnabled.Content = Convert.ToBoolean(uwf.UWF_Servicing2[uwf.WMI_PropertyName_ServicingEnabled].GetPropertyValue()) ? onTerm : offTerm;
                }
                catch (Exception ex)
                {
                    SetStatusBarText(cultureInfo, resourceManager, "Exception_Occurred", ex.HResult.ToString(), true);
                    hasFailed = true;
                }
            }
            #endregion

            #region UWF_Volume (We need admin rights)          
            try
            {
                // Check wheter the user is an administrator.
                if (isAdmin == true)
                {
                    // Get the list with protected volumes, e. g. C:.
                    List<UnifiedWriteFilter.ProtectedVolume> protectedVolumes = await GetProtectedVolumesAsync(computer);

                    // 1. Filtering out the protected volumes for the current session.
                    List<string> currentProtectedVolumes = ListProtectedVolumes(true, protectedVolumes);

                    // Output the current protected volumes.
                    if (currentProtectedVolumes != null && currentProtectedVolumes.Count > 0)
                    {
                        OutputProtectedVolume.Content = String.Empty;
                        foreach (string cpV in currentProtectedVolumes)
                        {
                            OutputProtectedVolume.Content += String.Format("{0} ", cpV);
                        }
                    }

                    // 2. Filtering out the protected volumes for the next session.
                    List<string> nextProtectedVolumes = ListProtectedVolumes(true, protectedVolumes);

                    // Output the current protected volumes.
                    if (nextProtectedVolumes != null && nextProtectedVolumes.Count > 0)
                    {
                        OutputNextProtectedVolume.Content = String.Empty;
                        foreach (string npV in nextProtectedVolumes)
                        {
                            OutputNextProtectedVolume.Content += String.Format("{0} ", npV);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatusBarText(cultureInfo, resourceManager, "Exception_Occurred", ex.HResult.ToString(), true);
                hasFailed = true;
            }
            #endregion

            return hasFailed;
        }
        #endregion

        /// <summary>
        /// Writes a status output to the application status bar. 
        /// </summary>
        /// <param name="text">The text content to be output.</param>
        /// <param name="isError">(optional) True if an error is output, otherwise true.
        /// This results in the output being displayed in a different colour.</param>
        private void SetStatusBarText(string text, bool isError = false)
        {
            OutputStatusBar.Text = text;
            string resString = isError ? "SecondaryHueMidOwnBrush" : "SecondaryHueLightOwnBrush";
            OutputStatusBarContainer.Background = (Brush)Application.Current.MainWindow.FindResource(resString);
        }

        /// <summary>
        /// Writes a status output to the application status bar in the respective language from the resource file. 
        /// </summary>
        /// <param name="cultureInfo">CultureInfo for a dedicated output language.</param>
        /// <param name="resourceManager">ResourceManager (dedicated output language).</param>
        /// <param name="ressourceX">Resource string to be loaded from the language file.</param>
        /// <param name="decHexError">Hex value as string.</param>
        /// <param name="isError">(optional)True if an error is output, otherwise true.
        /// This results in the output being displayed in a different colour.</param>
        private void SetStatusBarText(CultureInfo cultureInfo, ResourceManager resourceManager, string ressourceX, string decHexError, bool isError = false)
        {
            OutputStatusBar.Text = String.Format(resourceManager.GetString(ressourceX, cultureInfo), Helpers.GetHexByDecimal(decHexError));
            string resString = isError ? "SecondaryHueMidOwnBrush" : "SecondaryHueLightOwnBrush";
            OutputStatusBarContainer.Background = (Brush)Application.Current.MainWindow.FindResource(resString);
        }

        /// <summary>
        /// Helper function: Returns a simple string list of all protected volumes (of this or the next session), e.g. C D: E:.
        /// See also: GetProtectedVolumesAsync().
        /// </summary>
        /// <param name="current">true if the list for protected volumes of the current session should be delivered, otherwise false</param>
        /// <param name="pVs"></param>
        /// <returns></returns>
        private List<string> ListProtectedVolumes(bool current, List<UnifiedWriteFilter.ProtectedVolume> pVs)
        {
            // The simple string list with volumes, which is delivered as the result.
            List<string> volumesOnly = new List<string>();
            if (pVs != null && pVs.Count > 0)
            {
                // Go through the list of protected volumes (pVs=.
                foreach (UnifiedWriteFilter.ProtectedVolume pV in pVs)
                {
                    // Check wheter the volume is protected (in this or the next session).
                    if (pV.isProtected == true && pV.currentSession == current)
                    {
                        // Check whether the letter is already included in the list.
                        if (volumesOnly.Contains(pV.driveLetter) == false)
                        {
                            // Add the drive letter to the list. 
                            volumesOnly.Add(pV.driveLetter);
                        }
                    }
                }
            }
            // A simple, string list with the protected volumes, e.g. C: D: E:.
            return volumesOnly; 
        }

        /// <summary>
        /// An asynchronous method for collecting all required properties of the respective UWF classes.
        /// </summary>
        /// <param name="computer">DNS or NetBIOS name of the computer host for which you want to determine UWF properties.
        /// To get the properties of the local host, pass an empty string.</param>
        /// <returns>A Task object.</returns>
        public async Task<Helpers.AsyncReturnCode> CollectStatesAsync(string computer, CancellationToken token)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            // Todo: Creating an Async method instead of a Task.Run(). Access to the UWF classes are I\O operations, not CPU operations.

            // Create a task list for collecting all required properties.
            List<Task> tasks = new List<Task>
            {
                // For explanations on the use of CancelationToken see here: https://lbadri.wordpress.com/2016/10/04/cancellationtoken-with-task-run-and-wait/#comment-12223
                // An alternative use of await with Task.Run():
                // await Task.Run(() => uwf.GetWmiClassProperties(uwf.WMI_Query_UWF_Volume, ref uwf.UWF_Volume, token, computer, 0), token);
                
                /**
                 * Why await Task.Run() and not await without Task.Run for I/O.
                 * 
                 * Please read also here: https://www.pluralsight.com/guides/using-task-run-async-await
                 * await Task.Run => "It may be a quick and easy way to keep your application responsive, but it's not the most efficient use of system resources."
                 * 
                 * "But you may find that some classes in .NET or in third-party libraries only provide synchronous methods,
                 *  in which case you may be forced to use Task.Run to achieve asynchrony even though it is just an I/O operation."
                 */ 
                Task.Run(() => uwf.GetWmiClassProperties(uwf.WMI_Query_UWF_Volume, ref uwf.UWF_Volume, token, computer, 0), token),
                Task.Run(() => uwf.GetWmiClassProperties(uwf.WMI_Query_UWF_Overlay, ref uwf.UWF_Overlay, token, computer, 0), token),
                Task.Run(() => uwf.GetWmiClassProperties(uwf.WMI_Query_UWF_Servicing, ref uwf.UWF_Servicing, token, computer, 0), token),
                Task.Run(() => uwf.GetWmiClassProperties(uwf.WMI_Query_UWF_FilterStatus, ref uwf.UWF_Filters, token, computer, 0), token),
                Task.Run(() => uwf.GetWmiClassProperties(uwf.WMI_Query_UWF_Servicing, ref uwf.UWF_Servicing2, token, computer, 1), token),
                Task.Run(() => uwf.GetWmiClassProperties(uwf.WMI_Query_UWF_OverlayConfig, ref uwf.UWF_OverlayConfig, token, computer, 0), token),
                Task.Run(() => uwf.GetWmiClassProperties(uwf.WMI_Query_UWF_OverlayConfig, ref uwf.UWF_OverlayConfig2, token, computer, 1), token),
            };

            try
            {
                await Task.WhenAll(tasks); 
            }
            catch (AggregateException)
            {
                //foreach (var t in tasks)
                //{
                //    Console.WriteLine(t.Status);
                //}
                Console.WriteLine("AggregateException");
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine($"{nameof(OperationCanceledException)} thrown with message: {e.Message}");
                return Helpers.AsyncReturnCode.cancelled;
            }
            catch (Exception)
            {
                stopWatch.Stop();
                // Todo: Fehler in den Task.Run hier behandeln, z. B. in der Statusleiste ausgeben.
                Console.WriteLine("Task.Run() canceled.");
                //MessageBox.Show(String.Format("Task.Run: {0} {1}", e.Message, e.InnerException));
                return Helpers.AsyncReturnCode.error;
            }

            stopWatch.Stop();
            Console.WriteLine("Elapsed Time in {0}", stopWatch.ElapsedMilliseconds);
            return Helpers.AsyncReturnCode.successful;
        }

        /// <summary>
        /// Returns all properties of the UWF_Volume class as Task<TResult>.
        /// Information about protected drives can be extracted from it.
        /// See also: Helper function => ListProtectedVolumes().
        /// </summary>
        /// <param name="computer">DNS or NetBIOS name of the computer host for which you want to determine UWF properties.
        /// To get the properties of the local host, pass an empty string.</param>
        /// <returns></returns>
        public Task<List<UnifiedWriteFilter.ProtectedVolume>> GetProtectedVolumesAsync(string computer)
        {
            return Task.Run(() => uwf.GetProtectedVolumes(computer));          
        }

        /// <summary>
        /// Event: Fired when the UI button GetFilterStatus is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GetFilterStatus_Click(object sender, RoutedEventArgs e)
        {
            // Check whether the computer name has been entered.
            if (String.IsNullOrWhiteSpace(SearchComputer.Text))
            {
                _ = DialogHost.Show(NoComputerNameDialog.DialogContent, "uiNoComputerName");
                return;
            }

            // Get the filter status and displays it in the UI.
            FilterStatus();
        }

        /// <summary>
        /// Event: Occurs when the user clicks the CancelButton in DialogHost.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Change the text of the "Cancel" button in the DialogHost to "Please wait..." or something similar.
            // This is in case the Task.Run() threads hangs, as it can happen with timeouts, for example. 
            CancelLabel.Content = resourceManager.GetString("Cancel_Panding", cultureInfo);

            // Indicate the cancellation of running tasks by the user.
            tokenSource.Cancel();            
        }

        /// <summary>
        /// Determines the UWF filter status and displays it in the UI.
        /// </summary>
        private async void FilterStatus(bool checkInstallStateOnly = false)
        {
            bool operationErrorOccurred = false;
            try
            {
                #region DialogHost (show the work in progress dialog)
                _ = DialogHost.Show(CancelDialog.DialogContent, "uiCancelDialog");
                #endregion

                #region NLS, e.g. values for the terms "On" and "Off".
                // For the CultureInfo see under the class constructor: Instance to identify linguistically
                // appropriate terms on the respective language file (ressources.*.resx).
                string onTerm = resourceManager.GetString("UWF_ON", cultureInfo);
                string offTerm = resourceManager.GetString("UWF_OFF", cultureInfo);
                string uwfType_0 = resourceManager.GetString("UWF_Type_0", cultureInfo);
                string uwfType_1 = resourceManager.GetString("UWF_Type_1", cultureInfo);
                #endregion

                #region Cancellation token
                // Create a cancellation token and get it.
                tokenSource = new CancellationTokenSource();
                var token = tokenSource.Token;
                #endregion

                #region Computer (DNS or NetBIOS name)
                string searchComputer = SearchComputer.Text;
                if (SearchComputer.SelectedItem != null)
                {
                    // Check whether the selected item tag matches the "Search_Local_Tag" string from the resource file.
                    // In this case, the local host is used.
                    var selectedTag = SearchComputer.SelectedValue;
                    if (selectedTag != null)
                    {
                        if (selectedTag.ToString() == resourceManager.GetString("Search_Local_Tag", cultureInfo))
                        {
                            searchComputer = String.Empty;
                        }
                    }
                }
                computer = searchComputer.Trim();

                #region Add the computer to the Combobox.
                if (!SearchComputer.Items.Contains(computer) && (computer != ""))
                {
                    if (SearchComputer.Items.Count > MAX_SEARCHT_ITEMS)
                    {
                        // Remove the first added element (fifo) if it is not a local host element.
                        var firstItem = (ComboBoxItem)SearchComputer.Items[0];
                        if (firstItem.Content.ToString() != resourceManager.GetString("Search_Local", cultureInfo))
                        {
                            SearchComputer.Items.RemoveAt(0);
                        }
                        else
                        {
                            SearchComputer.Items.RemoveAt(1);
                        }
                    }
                    SearchComputer.Items.Add(computer);
                }
                #endregion

                #endregion

                #region UI
                // Clean the UI.
                CleanUI();
                SetStatusBarText(resourceManager.GetString("Operation_Progress"));
                CancelLabel.Content = String.Empty;
                #endregion

                #region Clear all existing property dictionaries and reinitialize them.
                if (uwf != null) uwf.ReInitFilterDictionaries();
                #endregion


                // TODO continue here.



                #region Check if the UWF feature is installed on the computer.
                Helpers.AsyncReturnCode arcFeatureCheck = await IsFilterInstalledAsync(computer, token);
                if (arcFeatureCheck == Helpers.AsyncReturnCode.cancelled)
                {
                    // Displays the error message that the operation was canceled manually (by the user).
                    SetStatusBarText(resourceManager.GetString("Cancelled_Operation", cultureInfo), true);
                    operationErrorOccurred = true;
                    return;
                }

                if (arcFeatureCheck == Helpers.AsyncReturnCode.successful)
                {
                    // Check now the obtained InstallState (isInstalled == true) of the UWF feature.
                    if (this.isUWFInstalled == false)
                    {
                        // The feature is not installed on the computer.
                        SetStatusBarText(String.Format(resourceManager.GetString("UWF_Filter_Is_Not_Installed", cultureInfo), computer), true);
                        operationErrorOccurred = true;
                        return;
                    }
                }
                #endregion

                #region Collect async all UWF properties.
                Helpers.AsyncReturnCode asyncReturnCode = await CollectStatesAsync(computer, token);
                if (asyncReturnCode == Helpers.AsyncReturnCode.cancelled)
                {
                    // Displays the error message that the operation was canceled manually (by the user).
                    SetStatusBarText(resourceManager.GetString("Cancelled_Operation", cultureInfo), true);
                    operationErrorOccurred = true;
                    return;
                }
                #endregion

                // Check whether an error has occurred in any of the tasks.
                if (uwf.LastError.HasFlag(UnifiedWriteFilter.ErrorCode.ErrorOccured) == true)
                {
                    // Go through all the uwf result dictionaries from the tasks and check them for possible errors.
                    // But first check if there is a connection error.
                    if (uwf.LastError.HasFlag(UnifiedWriteFilter.ErrorCode.TestConnectionFailed) == true)
                    {
                        // Displays an error message that the connection to the host was not possible.
                        SetStatusBarText(String.Format(resourceManager.GetString("Connection_Failed", cultureInfo), computer), true);
                        operationErrorOccurred = true;
                        return;
                    }

                    // Check now all uwf result dictionaries for errors...
                    #region UWF_Filter (Current und NextSession)
                    if (uwf.UWF_Filters[uwf.WMIQueryError_ErrorOccurred].GetPropertyValue().ToLower() == "true")
                    {
                        SetStatusBarText(cultureInfo, resourceManager, "Exception_Occurred", uwf.UWF_Filters[uwf.WMIQueryError_HResult].GetPropertyValue(), true);
                        operationErrorOccurred = true;
                        return;
                    }
                    #endregion
                    #region UWF_Overlay (CurrentSession and NextSession)
                    if (uwf.UWF_Overlay[uwf.WMIQueryError_ErrorOccurred].GetPropertyValue().ToLower() == "true")
                    {
                        SetStatusBarText(cultureInfo, resourceManager, "Exception_Occurred", uwf.UWF_Overlay[uwf.WMIQueryError_HResult].GetPropertyValue(), true);
                        operationErrorOccurred = true;
                        return;
                    }
                    #endregion
                    #region UWF_OverlayConfig (CurrentSession, it should be the instanceLevel 0)
                    if (uwf.UWF_OverlayConfig[uwf.WMIQueryError_ErrorOccurred].GetPropertyValue() == "true")
                    {
                        SetStatusBarText(cultureInfo, resourceManager, "Exception_Occurred", uwf.UWF_OverlayConfig[uwf.WMIQueryError_HResult].GetPropertyValue(), true);
                        operationErrorOccurred = true;
                        return;
                    }
                    #endregion
                    #region UWF_OverlayConfig2 (NextSession, it should be the instanceLevel 1)
                    if (uwf.UWF_OverlayConfig2[uwf.WMIQueryError_ErrorOccurred].GetPropertyValue().ToLower() == "true")
                    {
                        SetStatusBarText(cultureInfo, resourceManager, "Exception_Occurred", uwf.UWF_OverlayConfig2[uwf.WMIQueryError_HResult].GetPropertyValue(), true);
                        operationErrorOccurred = true;
                        return;
                    }
                    #endregion
                    #region UWF_Servicing (CurrentSession, it should be the instanceLevel 0)
                    if (uwf.UWF_Servicing[uwf.WMIQueryError_ErrorOccurred].GetPropertyValue().ToLower() == "true")
                    {
                        SetStatusBarText(cultureInfo, resourceManager, "Exception_Occurred", uwf.UWF_Servicing[uwf.WMIQueryError_HResult].GetPropertyValue(), true);
                        operationErrorOccurred = true;
                        return;
                    }
                    #endregion
                    #region UWF_Servicing2 (NextSession, it should be the instanceLevel 1)
                    if (uwf.UWF_Servicing2[uwf.WMIQueryError_ErrorOccurred].GetPropertyValue().ToLower() == "true")
                    {
                        SetStatusBarText(cultureInfo, resourceManager, "Exception_Occurred", uwf.UWF_Servicing2[uwf.WMIQueryError_HResult].GetPropertyValue(), true);
                        operationErrorOccurred = true;
                    }
                    #endregion
                    #region UWF_Volume
                    if (uwf.UWF_Volume[uwf.WMIQueryError_ErrorOccurred].GetPropertyValue().ToLower() == "true")
                    {
                        SetStatusBarText(cultureInfo, resourceManager, "Exception_Occurred", uwf.UWF_Volume[uwf.WMIQueryError_HResult].GetPropertyValue(), true);
                        operationErrorOccurred = true;
                        return;
                    }
                    #endregion
                }
                else
                {
                    // Output all uwf results...
                    operationErrorOccurred = await UIOutput_UWF_Status();
                }
            }
            finally
            {
                if (operationErrorOccurred == false)
                {
                    if ((resourceManager != null) && (cultureInfo != null))
                    {
                        SetStatusBarText(resourceManager.GetString("Operation_Successful", cultureInfo), false);
                    }
                }

                // Close the cancel dialog box.
                // Example (alternative): DialogHost.CloseDialogCommand.Execute(null, null);
                CancelDialog.IsOpen = false;
            }
        }

        /// <summary>
        /// Checks if the UWF feature is installed on the target computer.
        /// </summary>
        /// <returns>Returns true if the filter is installed, false otherwise.</returns>
        private async Task<Helpers.AsyncReturnCode> IsFilterInstalledAsync(string computer, CancellationToken token)
        {
            // Filter feature install states.
            // See here for more informations: https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/win32-optionalfeature
            // const string isDisable = "2";
            // const string isAbsent = "3";
            const string isEnabled = "1";
            const string isUnknown = "4";
           
            string outPropertyValue = isUnknown;
            isUWFInstalled = false;

            try
            {
                await Task.Run(() => uwf.GetWmiClassProperty(uwf.WMI_Query_WIN32_OperationalFeature_UWF, "InstallState", ref outPropertyValue, computer, uwf.WMI_NAMESPACE_ROOT_CIMV2), token);
            }
            catch (AggregateException)
            {
                Console.WriteLine("AggregateException");
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine($"{nameof(OperationCanceledException)} thrown with message: {e.Message}");
                return Helpers.AsyncReturnCode.cancelled;
            }
            catch (Exception)
            {
                // Todo: Fehler in den Task.Run hier behandeln, z. B. in der Statusleiste ausgeben.
                Console.WriteLine("Task.Run() canceled.");
                //MessageBox.Show(String.Format("Task.Run: {0} {1}", e.Message, e.InnerException));
                return Helpers.AsyncReturnCode.error;
            }

            isUWFInstalled = (outPropertyValue == isEnabled);
            return Helpers.AsyncReturnCode.successful;
        }

        /// <summary>
        /// Method which resets Dialog Controls to an initial value.
        /// </summary>
        /// <param name="dialogName"></param>
        private void ResetUiDialog(string dialogName)
        {
            switch (dialogName)
            {
                case "uiConfirmInvokeFilterMethod":
                    uiUserInformedCbx.IsChecked = false;
                    uiExecFilterActionBtn.IsEnabled = false;
                    uiComputerRestartCbx.IsChecked = false;
                    uiComputerRestartCbx.IsEnabled = true;
                    break;
            }
        }

        /// <summary>
        /// Event: An event that is fired when the admin clicks the enable filter button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EnableFilterButton_Click(object sender, RoutedEventArgs e)
        {
            string dialogName = "uiConfirmInvokeFilterMethod";

            // Check whether the computer name has been entered.
            if (String.IsNullOrWhiteSpace(SearchComputer.Text))
            {
                _ = DialogHost.Show(NoComputerNameDialog.DialogContent, "uiNoComputerName");
                return;
            }

            // Set the current filter action (over binding) in the control Tag property of the action button. 
            ((DataViewModel)DataContext).LastFilterAction = UWFFilterMethodName.Enable;

            // Set the user dialog to defaults.
            ResetUiDialog(dialogName);

            #region Questions to the Admin
            // Ask admin if he really wants to enable the filter.
            string msgEnablingFilter = resourceManager.GetString("UWF_Invoke_Filter_Enable", cultureInfo);
            computer = SearchComputer.Text;
            string msgComputerName = computer;
            if (computer == resourceManager.GetString("Search_Local", cultureInfo))
            {
                msgComputerName = String.Format(resourceManager.GetString("This_Computer", cultureInfo));
                // Don't restart this machine.
                uiComputerRestartCbx.IsEnabled = false;
            }
            uiQuestInvokeFilterMethod.Content = String.Format(resourceManager.GetString("UWF_Question_Invoke_Filter_Method", cultureInfo), msgComputerName, msgEnablingFilter);

            // Ask the admin if he has informed the user about this action.
            string msgUserInformed = String.Format(resourceManager.GetString("UWF_Question_Invoke_Filter_Method_User_Informed", cultureInfo),
                                                    String.Format(resourceManager.GetString("UWF_Invoke_Filter_Activation", cultureInfo)));
            uiUserInformedCbx.Content = msgUserInformed;
            #endregion

            // Show the confirmation dialog.
            _ = DialogHost.Show(ConfirmInvokeFilterMethod.DialogContent, dialogName);
        }

        /// <summary>
        /// Event: An event that is fired when the admin clicks the disable filter button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisableFilterButton_Click(object sender, RoutedEventArgs e)
        {
            string dialogName = "uiConfirmInvokeFilterMethod";

            // Check whether the computer name has been entered.
            if (String.IsNullOrWhiteSpace(SearchComputer.Text))
            {
                _ = DialogHost.Show(NoComputerNameDialog.DialogContent, "uiNoComputerName");
                return;
            }

            // Set the current filter action (over binding) in the control Tag property of the action button. 
            ((DataViewModel)DataContext).LastFilterAction = UWFFilterMethodName.Disable;

            // Set the user dialog to defaults.
            ResetUiDialog(dialogName);

            #region Questions to the Admin
            // Ask admin if he really wants to disable the filter.
            string msgDisablingFilter = resourceManager.GetString("UWF_Invoke_Filter_Disable", cultureInfo);
            computer = SearchComputer.Text;
            string msgComputerName = computer;
            if (computer == resourceManager.GetString("Search_Local", cultureInfo))
            {
                msgComputerName = String.Format(resourceManager.GetString("This_Computer", cultureInfo));
                // Don't restart this machine.
                uiComputerRestartCbx.IsEnabled = false;
            }
            uiQuestInvokeFilterMethod.Content = String.Format(resourceManager.GetString("UWF_Question_Invoke_Filter_Method", cultureInfo), msgComputerName, msgDisablingFilter);
            
            // Ask the admin if he has informed the user about this action.
            string msgUserInformed = String.Format(resourceManager.GetString("UWF_Question_Invoke_Filter_Method_User_Informed", cultureInfo),
                                                    String.Format(resourceManager.GetString("UWF_Invoke_Filter_Deactivation", cultureInfo)));
            uiUserInformedCbx.Content = msgUserInformed;
            #endregion

            // Show the confirmation dialog.
            _ = DialogHost.Show(ConfirmInvokeFilterMethod.DialogContent, dialogName);
        }

        /// <summary>
        /// Event: Is fired when the admin clicks on the "User has been informed"-checkbox.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void uiUserInformedCbx_Click(object sender, RoutedEventArgs e)
        {
            // Enable the button to activate the UWF filter.
            uiExecFilterActionBtn.IsEnabled = (uiUserInformedCbx.IsChecked == true) ? true : false;
        }

        /// <summary>
        /// Event: Is fired when the admin clicks on the Yes-Button (filter activation, deactivation).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void uiExecFilterActionBtn_Click(object sender, RoutedEventArgs e)
        {           
            // MessageBox.Show(String.Format("uiExecFilterAction: Method: {1}, Restart computer: {0}", ((DataViewModel)DataContext).RestartComputer.ToString(), ((DataViewModel)DataContext).LastFilterAction));
            
                       
            // TODO here check if the UWF Feature is installed.


            /**
             * Invoke the filter method: Which LastFilterAction is executed
             * is decided by the control binding in the respective method, e.g. for the action Disable (UWFFilterMethodName.Disable)
             * see in DisableFilterButton_Click().
             */
            uwf.InvokeFilterMethodAsynch(((DataViewModel)DataContext).LastFilterAction, computer);

            // Close the dialog window.
            ConfirmInvokeFilterMethod.IsOpen = false;

            // Restart the remote computer?
            bool restartComputer = ((DataViewModel)DataContext).RestartComputer;
            if (restartComputer == true)
            {
                uwf.InvokeFilterMethodAsynch(UWFFilterMethodName.RestartPC, computer);
            }
            else
            {
                // Get the filter status and displays it in the UI.
                FilterStatus();
            }
        }

        /// <summary>
        /// Event: Is fired when the admin clicks on the Cancel-Button (filter action). 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void uiCancFilterActionBtn_Click(object sender, RoutedEventArgs e)
        {
            ConfirmInvokeFilterMethod.IsOpen = false;
        }

        /// <summary>
        /// Event: Is fired when the admi clicks on the project info link.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProjectSiteHyperlink_Click(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}