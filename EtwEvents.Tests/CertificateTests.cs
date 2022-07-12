﻿using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using KdSoft.EtwEvents;
using Microsoft.DotNet.PlatformAbstractions;
using Xunit;
using Xunit.Abstractions;

namespace EtwEvents.Tests
{
    public class CertificateTests
    {
        readonly ITestOutputHelper _output;

        public CertificateTests(ITestOutputHelper output) {
            this._output = output;
        }

        [Fact]
        public void Load() {
            //Create new X509 store from local certificate store.
            X509Store store = new X509Store("MY", StoreLocation.LocalMachine);
            store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite);

            //Output store information.
            _output.WriteLine("Store Information");
            _output.WriteLine("Number of certificates in the store: {0}", store.Certificates.Count);
            _output.WriteLine("Store location: {0}", store.Location);
            _output.WriteLine("Store name: {0} {1}", store.Name, Environment.NewLine);

            //Put certificates from the store into a collection so user can select one.
            X509Certificate2Collection fcollection = (X509Certificate2Collection)store.Certificates;
            X509Certificate2Collection collection = X509Certificate2UI.SelectFromCollection(fcollection, "Select an X509 Certificate", "Choose a certificate to examine.", X509SelectionFlag.SingleSelection);
            X509Certificate2 certificate = collection[0];
            X509Certificate2UI.DisplayCertificate(certificate);

            //Output chain information of the selected certificate.
            var ch = new X509Chain { ChainPolicy = CertUtils.GetClientCertPolicy() };
            //ch.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            bool valid = ch.Build(certificate);
            _output.WriteLine("Chain Information - valid: {0}", valid);
            _output.WriteLine("Chain revocation flag: {0}", ch.ChainPolicy.RevocationFlag);
            _output.WriteLine("Chain revocation mode: {0}", ch.ChainPolicy.RevocationMode);
            _output.WriteLine("Chain verification flag: {0}", ch.ChainPolicy.VerificationFlags);
            _output.WriteLine("Chain verification time: {0}", ch.ChainPolicy.VerificationTime);
            _output.WriteLine("Chain status length: {0}", ch.ChainStatus.Length);
            _output.WriteLine("Chain application policy count: {0}", ch.ChainPolicy.ApplicationPolicy.Count);
            _output.WriteLine("Chain certificate policy count: {0} {1}", ch.ChainPolicy.CertificatePolicy.Count, Environment.NewLine);

            //Output chain element information.
            _output.WriteLine("Chain Element Information");
            _output.WriteLine("Number of chain elements: {0}", ch.ChainElements.Count);
            _output.WriteLine("Chain elements synchronized? {0}", ch.ChainElements.IsSynchronized);

            foreach (X509ChainElement element in ch.ChainElements) {
                _output.WriteLine("");
                _output.WriteLine("Element issuer name: {0}", element.Certificate.Issuer);
                _output.WriteLine("Element certificate valid until: {0}", element.Certificate.NotAfter);
                _output.WriteLine("Element certificate is valid: {0}", element.Certificate.Verify());
                _output.WriteLine("Element error status length: {0}", element.ChainElementStatus.Length);
                _output.WriteLine("Element information: {0}", element.Information);
                _output.WriteLine("Number of element extensions: {0}", element.Certificate.Extensions.Count);
                if (ch.ChainStatus.Length > 1) {
                    for (int index = 0; index < element.ChainElementStatus.Length; index++) {
                        var cst = element.ChainElementStatus[index];
                        _output.WriteLine("Chain status {0}: {1}", cst.Status, cst.StatusInformation);
                    }
                }
            }
            store.Close();
        }

        [Fact]
        public void InstallCerts() {
            var filesPath = Path.Combine(TestUtils.ProjectDir!, "Files");
            var rootCert = new X509Certificate2(Path.Combine(filesPath, "Kd-Soft.crt"));

            // first uninstall
            using (var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine)) {
                store.Open(OpenFlags.ReadWrite);
                store.Remove(rootCert);
            }

            // cert should not validate without root cert
            var serverCert = new X509Certificate2(Path.Combine(filesPath, "server.kd-soft.net.p12"), "humpty_dumpty", X509KeyStorageFlags.PersistKeySet);
            var serverChain = new X509Chain { ChainPolicy = new X509ChainPolicy { RevocationMode = X509RevocationMode.NoCheck} };
            bool valid = serverChain.Build(serverCert);
            if (!valid) {
                foreach (var cst in serverChain.ChainStatus) {
                    _output.WriteLine("{0}: {1}", cst.Status, cst.StatusInformation);
                }
            }
            Assert.False(valid);

            var clientCert = new X509Certificate2(Path.Combine(filesPath, "client.p12"), "humpty_dumpty", X509KeyStorageFlags.PersistKeySet);
            var clientChain = new X509Chain { ChainPolicy = new X509ChainPolicy { RevocationMode = X509RevocationMode.NoCheck } };
            valid = clientChain.Build(clientCert);
            if (!valid) {
                foreach (var cst in clientChain.ChainStatus) {
                    _output.WriteLine("{0}: {1}", cst.Status, cst.StatusInformation);
                }
            }
            Assert.False(valid);

            CertUtils.InstallMachineCertificate(rootCert);

            valid = serverChain.Build(serverCert);
            Assert.True(valid);

            valid = clientChain.Build(clientCert);
            Assert.True(valid);
        }
    }
}