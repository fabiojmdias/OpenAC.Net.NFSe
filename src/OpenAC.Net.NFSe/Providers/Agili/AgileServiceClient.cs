using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OpenAC.Net.NFSe.Providers.Agili
{
    public class AgileServiceClient : NFSeRestServiceClient, IServiceClient
    {
        private readonly string _contentType;
        public AgileServiceClient(ProviderBase provider, TipoUrl tipoUrl) : base(provider, tipoUrl)
        {
            //AuthenticationHeader = "AUTHORIZATION";
            _contentType = "application/xml";
        }

        public string ConsultarNFSeRps(string cabec, string msg)
        {
            return Post("ConsultarNfseRps", msg, _contentType);
        }

        public string ConsultarNFSe(string cabec, string msg)
        {
            return Post("ConsultarNfseFaixa", msg, _contentType);
        }

        public string EnviarSincrono(string cabec, string msg)
        {
            return Post("GerarNfse", msg, _contentType);
        }

        #region not implemented

        public string CancelarNFSe(string cabec, string msg)
        {
            throw new NotImplementedException();
        }

        public string CancelarNFSeLote(string cabec, string msg)
        {
            throw new NotImplementedException();
        }

        public string ConsultarLoteRps(string cabec, string msg)
        {
            throw new NotImplementedException();
        }

        public string ConsultarSequencialRps(string cabec, string msg)
        {
            throw new NotImplementedException();
        }

        public string ConsultarSituacao(string cabec, string msg)
        {
            throw new NotImplementedException();
        }

        public string ConsultarUrlVisualizacaoNfse(string cabec, string msg)
        {
            throw new NotImplementedException();
        }

        public string Enviar(string cabec, string msg)
        {
            throw new NotImplementedException();
        }

        public string SubstituirNFSe(string cabec, string msg)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
