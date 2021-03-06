﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.R.Host.Client {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.R.Host.Client.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Previous R session terminated unexpectedly, and its global workspace is currently being saved.
        ///Do you want to abort this operation and start current session immediately?.
        /// </summary>
        internal static string AbortRDataAutosave {
            get {
                return ResourceManager.GetString("AbortRDataAutosave", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The security certificate presented by the Remote R Services does not allow us to prove that you are indeed connecting to the machine {0}.
        ///
        ///This should never happen with a production Remote R Services, so please check with your server administrator.
        ///
        ///If you using a test Remote R Server with a self-signed certificate and are certain about the remote machine security, click OK, otherwise cancel the connection..
        /// </summary>
        internal static string CertificateSecurityWarning {
            get {
                return ResourceManager.GetString("CertificateSecurityWarning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The machine appears to be online, but the Remote R Service is not running..
        /// </summary>
        internal static string Error_BrokerNotRunning {
            get {
                return ResourceManager.GetString("Error_BrokerNotRunning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Host did not respond to a ping.
        ///The machine may be offline or the network has been disconnected.
        ///Error: {0}.
        /// </summary>
        internal static string Error_HostNotResponding {
            get {
                return ResourceManager.GetString("Error_HostNotResponding", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Specified R interpreter was not found.
        /// </summary>
        internal static string Error_InterpreterNotFound {
            get {
                return ResourceManager.GetString("Error_InterpreterNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Remote machine does not have certificate installed for the TLS with the Remote R Service..
        /// </summary>
        internal static string Error_NoBrokerCertificate {
            get {
                return ResourceManager.GetString("Error_NoBrokerCertificate", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No R Interpreters installed.
        /// </summary>
        internal static string Error_NoRInterpreters {
            get {
                return ResourceManager.GetString("Error_NoRInterpreters", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Web request has timed out..
        /// </summary>
        internal static string Error_OperationTimedOut {
            get {
                return ResourceManager.GetString("Error_OperationTimedOut", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This session already has an active client connection.
        /// </summary>
        internal static string Error_PipeAlreadyConnected {
            get {
                return ResourceManager.GetString("Error_PipeAlreadyConnected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The following exception occurred during initialization of R session, and the session has been terminated:
        ///
        ///{0}.
        /// </summary>
        internal static string Error_SessionInitialization {
            get {
                return ResourceManager.GetString("Error_SessionInitialization", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to enable workspace auto-saving: {0}.
        /// </summary>
        internal static string Error_SessionInitializationAutosave {
            get {
                return ResourceManager.GetString("Error_SessionInitializationAutosave", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to set R locale to codepage {0}: {1}.
        /// </summary>
        internal static string Error_SessionInitializationCodePage {
            get {
                return ResourceManager.GetString("Error_SessionInitializationCodePage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to set CRAN mirror to {0}: {1}.
        /// </summary>
        internal static string Error_SessionInitializationMirror {
            get {
                return ResourceManager.GetString("Error_SessionInitializationMirror", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to set options(): {0}.
        /// </summary>
        internal static string Error_SessionInitializationOptions {
            get {
                return ResourceManager.GetString("Error_SessionInitializationOptions", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to R broker process did not start:  {0}.
        /// </summary>
        internal static string Error_UnableToStartBrokerException {
            get {
                return ResourceManager.GetString("Error_UnableToStartBrokerException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to R host process did not start:  {0}.
        /// </summary>
        internal static string Error_UnableToStartHostException {
            get {
                return ResourceManager.GetString("Error_UnableToStartHostException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unknown error.
        /// </summary>
        internal static string Error_UnknownError {
            get {
                return ResourceManager.GetString("Error_UnknownError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to HTTP error while creating session: {0}.
        /// </summary>
        internal static string HttpErrorCreatingSession {
            get {
                return ResourceManager.GetString("HttpErrorCreatingSession", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Interactive Window is disconnected from R Session..
        /// </summary>
        internal static string RHostDisconnected {
            get {
                return ResourceManager.GetString("RHostDisconnected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Connecting to R Workspace failed.
        ///Reason: {0}.
        /// </summary>
        internal static string RSessionProvider_ConnectionFailed {
            get {
                return ResourceManager.GetString("RSessionProvider_ConnectionFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The supplied TLS certificate is not trusted: {0}.
        /// </summary>
        internal static string Trace_UntrustedCertificate {
            get {
                return ResourceManager.GetString("Trace_UntrustedCertificate", resourceCulture);
            }
        }
    }
}
