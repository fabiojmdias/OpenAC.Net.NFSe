using OpenAC.Net.NFSe.Nota;
using OpenAC.Net.NFSe.Configuracao;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenAC.Net.NFSe.Providers.Agili;
using OpenAC.Net.Core.Extensions;
using System.ServiceModel.Channels;
using System.Xml;

namespace OpenAC.Net.NFSe.Providers
{
    internal sealed class ProviderAgili : ProviderBase
    {
        public ProviderAgili(ConfigNFSe config, OpenMunicipioNFSe municipio) : base(config, municipio)
        {
            Name = "Agili";
        }

        private string MontarXmlIdentificacaoPrestador()
        {
            var result = new StringBuilder();
            result.Append("<IdentificacaoPrestador>");
            result.Append($"<ChaveDigital>{Configuracoes.PrestadorPadrao.NumeroEmissorRps}</ChaveDigital>");
            result.Append("<CpfCnpj>");
            result.Append($"<Cnpj>{Configuracoes.PrestadorPadrao.CpfCnpj}</Cnpj>");
            result.Append("</CpfCnpj>");
            result.Append($"<InscricaoMunicipal>{Configuracoes.PrestadorPadrao.InscricaoMunicipal}</InscricaoMunicipal>");
            result.Append("</IdentificacaoPrestador>");
            return result.ToString();
        }

        private string MontarXmlIdentificacaoRps(NotaServico nota)
        {
            var result = new StringBuilder();
            result.Append("<Rps>");
            result.Append("<IdentificacaoRps>");
            result.Append($"<Numero>{nota.IdentificacaoRps.Numero}</Numero>");
            result.Append($"<Serie>{nota.IdentificacaoRps.Serie}</Serie>");
            int tipoRpsAgili = -2; //RPS
            switch (nota.IdentificacaoRps.Tipo)
            {
                case TipoRps.NFConjugada:
                    tipoRpsAgili = -4;
                    break;
                case TipoRps.Cupom:
                    tipoRpsAgili = -5;
                    break;
                default:
                    tipoRpsAgili = -2;
                    break;
            }
            result.Append($"<Tipo>{tipoRpsAgili}</Tipo>");
            result.Append("</IdentificacaoRps>");
            result.Append($"<DataEmissao>{nota.IdentificacaoRps.DataEmissao:yyyy-MM-dd}</DataEmissao>");
            result.Append($"</Rps>");
            return result.ToString();
        }

        private string MontarXmlDadosTomador()
        {
            var result = new StringBuilder();
            result.Append($"");
            result.Append($"");
            result.Append($"");
            result.Append($"");
            result.Append($"");
            result.Append($"");
continua aqui...

            /*
    <DadosTomador>
      <IdentificacaoTomador>
        <CpfCnpj>
          <Cnpj>04152437000100</Cnpj>
        </CpfCnpj>
      <InscricaoMunicipal>110884</InscricaoMunicipal>
      </IdentificacaoTomador>
      <RazaoSocial>TOMADOR NFS-e - AL͑UOTA ESPECIAL</RazaoSocial>
      <LocalEndereco>1</LocalEndereco>
      <Endereco>
        <TipoLogradouro>Rua</TipoLogradouro>
        <Logradouro>CARMEM ALVES FONSECA</Logradouro>
        <Numero>150</Numero>
        <Bairro>CENTRO</Bairro>
        <Municipio>
          <CodigoMunicipioIBGE>5000203</CodigoMunicipioIBGE>
          <Descricao>Água Clara</Descricao>
          <Uf>MS</Uf>
        </Municipio>
        <Pais>
          <CodigoPaisBacen>01058</CodigoPaisBacen>
          <Descricao>Brasil</Descricao>
        </Pais>
        <Cep>79680000</Cep>
      </Endereco>
      <Contato>
        <Email>tomador@aliquotaespecial.com.br</Email>
      </Contato>
    </DadosTomador>
             
             
             */
        }

        protected override void PrepararConsultarNFSeRps(RetornoConsultarNFSeRps retornoWebservice, NotaServicoCollection notas)
        {
            var message = new StringBuilder();
            message.Append("<ConsultarNfseRpsEnvio xmlns=\"http://www.agili.com.br/nfse_v_1.00.xsd\">");
            message.Append($"<UnidadeGestora>{Municipio.Cnpj}</UnidadeGestora>");
            message.Append("<IdentificacaoRps>");
            message.Append($"<Numero>{retornoWebservice.NumeroRps}</Numero>");
            message.Append($"<Serie>{retornoWebservice.Serie}</Serie>");
            message.Append("<Tipo>-2</Tipo>"); //Tipo Rps
            message.Append("</IdentificacaoRps>");
            message.Append(MontarXmlIdentificacaoPrestador());
            message.Append("<Versao>1.00</Versao>");
            message.Append($"</ConsultarNfseRpsEnvio>");
            retornoWebservice.XmlEnvio = message.ToString();
        }

        protected override void AssinarConsultarNFSeRps(RetornoConsultarNFSeRps retornoWebservice)
        {
            return;
        }

        protected override bool PrecisaValidarSchema(TipoUrl tipo)
        {
            //return false;
            return true;    //Talvez pode ser desativado
        }

        protected override string GetSchema(TipoUrl tipo)
        {
            return "WebAPI-NFSe.xsd";
        }

        protected override IServiceClient GetClient(TipoUrl tipo)
        {
            return new AgileServiceClient(this, tipo);
        }

        protected override string GerarCabecalho()
        {
            return string.Empty;
        }

        protected override void TratarRetornoConsultarNFSeRps(RetornoConsultarNFSeRps retornoWebservice, NotaServicoCollection notas)
        {
            var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);
            MensagemErro(retornoWebservice, xmlRet, "ConsultarNfseRpsResposta");
            if (retornoWebservice.Erros.Any()) return;
            var compNfse = xmlRet.ElementAnyNs("ConsultarNfseRpsResposta")?.ElementAnyNs("CompNfse");
            if (compNfse == null)
            {
                retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "Nota Fiscal não encontrada! (CompNfse)" });
                return;
            }
            //TODO: Dias - Falta validar esse método
            /*
            // Analisa mensagem de retorno

            var nfse = compNfse.ElementAnyNs("Nfse").ElementAnyNs("InfNfse");
            var numeroNFSe = nfse.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty;
            var chaveNFSe = nfse.ElementAnyNs("CodigoVerificacao")?.GetValue<string>() ?? string.Empty;
            var dataNFSe = nfse.ElementAnyNs("DataEmissao")?.GetValue<DateTime>() ?? DateTime.Now;
            var numeroRps = nfse.ElementAnyNs("DeclaracaoPrestacaoServico")?
                                .ElementAnyNs("InfDeclaracaoPrestacaoServico")?
                                .ElementAnyNs("Rps")?
                                .ElementAnyNs("IdentificacaoRps")?
                                .ElementAnyNs("Numero").GetValue<string>() ?? string.Empty;

            GravarNFSeEmDisco(compNfse.AsString(true), $"NFSe-{numeroNFSe}-{chaveNFSe}-.xml", dataNFSe);

            // Carrega a nota fiscal na coleção de Notas Fiscais
            var nota = notas.FirstOrDefault(x => x.IdentificacaoRps.Numero == numeroRps);

            if (nota == null)
            {
                nota = notas.Load(compNfse.ToString());
            }
            else
            {
                nota.IdentificacaoNFSe.Numero = numeroNFSe;
                nota.IdentificacaoNFSe.Chave = chaveNFSe;
                nota.IdentificacaoNFSe.DataEmissao = dataNFSe;
                nota.XmlOriginal = compNfse.ToString();
            }

            retornoWebservice.Nota = nota;
            retornoWebservice.Sucesso = true;
             
             
             */
        }

        private void MensagemErro(RetornoWebservice retornoWs, XContainer xmlRet, string xmlTag)
        {
            var mensagens = xmlRet?.ElementAnyNs(xmlTag);
            mensagens = mensagens?.ElementAnyNs("ListaMensagemRetorno");
            if (mensagens != null)
            {
                foreach (var mensagem in mensagens.ElementsAnyNs("MensagemRetorno"))
                {
                    var evento = new Evento
                    {
                        Codigo = mensagem?.ElementAnyNs("Codigo")?.GetValue<string>() ?? string.Empty,
                        Descricao = mensagem?.ElementAnyNs("Mensagem")?.GetValue<string>() ?? string.Empty,
                        Correcao = mensagem?.ElementAnyNs("Correcao")?.GetValue<string>() ?? string.Empty
                    };
                    retornoWs.Erros.Add(evento);
                }
            }
            //Aparentement não precisa desse bloco
            //mensagens = xmlRet?.ElementAnyNs(xmlTag);
            //mensagens = mensagens?.ElementAnyNs("ListaMensagemRetornoLote");
            //if (mensagens == null) return;
            //{
            //    foreach (var mensagem in mensagens.ElementsAnyNs("MensagemRetorno"))
            //    {
            //        var evento = new Evento
            //        {
            //            Codigo = mensagem?.ElementAnyNs("Codigo")?.GetValue<string>() ?? string.Empty,
            //            Descricao = mensagem?.ElementAnyNs("Mensagem")?.GetValue<string>() ?? string.Empty,
            //            IdentificacaoRps = new IdeRps()
            //            {
            //                Numero = mensagem?.ElementAnyNs("IdentificacaoRps")?.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty,
            //                Serie = mensagem?.ElementAnyNs("IdentificacaoRps")?.ElementAnyNs("Serie")?.GetValue<string>() ?? string.Empty,
            //                Tipo = mensagem?.ElementAnyNs("IdentificacaoRps")?.ElementAnyNs("Tipo")?.GetValue<TipoRps>() ?? TipoRps.RPS,
            //            }
            //        };
            //        retornoWs.Erros.Add(evento);
            //    }
            //}
        }

        protected override void PrepararConsultarNFSe(RetornoConsultarNFSe retornoWebservice)
        {
            var message = new StringBuilder();
            message.Append("<ConsultarNfseFaixaEnvio xmlns=\"http://www.agili.com.br/nfse_v_1.00.xsd\">");
            message.Append($"<UnidadeGestora>{Municipio.Cnpj}</UnidadeGestora>");
            message.Append(MontarXmlIdentificacaoPrestador());
            message.Append($"<NumeroNfseInicial>{retornoWebservice.NumeroNFse}</NumeroNfseInicial>");
            message.Append($"<NumeroNfseFinal>{retornoWebservice.NumeroNFse}</NumeroNfseFinal>");
            message.Append("<Versao>1.00</Versao>");
            message.Append("</ConsultarNfseFaixaEnvio>");
            retornoWebservice.XmlEnvio = message.ToString();
        }

        protected override void AssinarConsultarNFSe(RetornoConsultarNFSe retornoWebservice)
        {
            return;
        }

        protected override void TratarRetornoConsultarNFSe(RetornoConsultarNFSe retornoWebservice, NotaServicoCollection notas)
        {
            // Analisa mensagem de retorno
            var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);
            MensagemErro(retornoWebservice, xmlRet, "ConsultarNfseFaixaEnvioResposta");
            if (retornoWebservice.Erros.Any()) return;

            var retornoLote = xmlRet.ElementAnyNs("ConsultarNfseFaixaEnvioResposta");
            var listaNfse = retornoLote?.ElementAnyNs("ListaNfse");
            if (listaNfse == null)
            {
                retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "Lista de NFSe não encontrada! (ListaNfse)" });
                return;
            }
            // TODO: Dias - Falta validar esse método
            //var notasServico = new List<NotaServico>();

            //foreach (var compNfse in listaNfse.ElementsAnyNs("CompNfse"))
            //{
            //    // Carrega a nota fiscal na coleção de Notas Fiscais
            //    var nota = LoadXml(compNfse.AsString());

            //    GravarNFSeEmDisco(compNfse.AsString(true), $"NFSe-{nota.IdentificacaoNFSe.Numero}-{nota.IdentificacaoNFSe.Chave}-.xml", nota.IdentificacaoNFSe.DataEmissao);

            //    notasServico.Add(nota);
            //    notas.Add(nota);
            //}

            //retornoWebservice.ProximaPagina = listaNfse.ElementAnyNs("ProximaPagina")?.GetValue<int>() ?? 0;
            //retornoWebservice.Notas = notasServico.ToArray();
        }

        protected override void PrepararEnviarSincrono(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
        {
            // Fabio 09/09/2023 18:02 - O envio não será feito por Lote.
            if (notas.Count == 0)
            {
                retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "RPS não informado." });
                return;
            }
            var xmlRpsEnvio = new StringBuilder();
            xmlRpsEnvio.Append("<GerarNfseEnvio xmlns=\"http://www.agili.com.br/nfse_v_1.00.xsd\">");
            xmlRpsEnvio.Append($"<UnidadeGestora>{Municipio.Cnpj}</UnidadeGestora>");
            xmlRpsEnvio.Append("<DeclaracaoPrestacaoServico>");
            xmlRpsEnvio.Append(MontarXmlIdentificacaoPrestador());
            xmlRpsEnvio.Append(MontarXmlIdentificacaoRps(notas[0]));

            xmlRpsEnvio.Append($"");  chamar dados tomador
            xmlRpsEnvio.Append($"");
            xmlRpsEnvio.Append($"");
            xmlRpsEnvio.Append($"");
            xmlRpsEnvio.Append($"");
            xmlRpsEnvio.Append($"");
            xmlRpsEnvio.Append($"");
            xmlRpsEnvio.Append($"");
            xmlRpsEnvio.Append($"");
            xmlRpsEnvio.Append($"");
            xmlRpsEnvio.Append($"");
            xmlRpsEnvio.Append($"");
            xmlRpsEnvio.Append($"");




            /*
             * 




    <DadosIntermediario>
      <IdentificacaoIntermediario>
        <CpfCnpj>
          <Cnpj>20502752000168</Cnpj>
        </CpfCnpj>
      <InscricaoMunicipal>110885</InscricaoMunicipal>
      </IdentificacaoIntermediario>
      <RazaoSocial>INTERMEDIÁRIO NFS-e - AL͑UOTA ESPECIAL</RazaoSocial>
    </DadosIntermediario>

    <RegimeEspecialTributacao>
      <Codigo>-6</Codigo>
      <Descricao>MicroempresᲩo e empresa de pequeno porte (ME EPP)</Descricao>
    </RegimeEspecialTributacao>
    <OptanteSimplesNacional>1</OptanteSimplesNacional>
    <OptanteMEISimei>0</OptanteMEISimei>
    <ISSQNRetido>1</ISSQNRetido>
    <ResponsavelISSQN>
      <Codigo>-2</Codigo>
      <Descricao>IntermediᲩo do serviço</Descricao>
    </ResponsavelISSQN>
    <CodigoAtividadeEconomica>01628.03</CodigoAtividadeEconomica>
    <ExigibilidadeISSQN>
      <Codigo>-1</Codigo>
      <Descricao>Exig�l</Descricao>
    </ExigibilidadeISSQN>
    <MunicipioIncidencia>
      <CodigoMunicipioIBGE>5000203</CodigoMunicipioIBGE>
      <Descricao>Água Clara</Descricao>
      <Uf>MS</Uf>
    </MunicipioIncidencia>
    <ValorServicos>1000.00</ValorServicos>
    <ValorDescontos>0</ValorDescontos>
    <ValorPis>0</ValorPis>
    <ValorCofins>0</ValorCofins>
    <ValorInss>0</ValorInss>
    <ValorIrrf>0</ValorIrrf>
    <ValorCsll>0</ValorCsll>
    <ValorOutrasRetencoes>0</ValorOutrasRetencoes>
    <ValorBaseCalculoISSQN>1000.00</ValorBaseCalculoISSQN>
    <AliquotaISSQN>3.0</AliquotaISSQN>
    <ValorISSQNCalculado>50.00</ValorISSQNCalculado>
    <ValorISSQNRecolher>0</ValorISSQNRecolher>
    <ValorDeducaoConstCivil>0.00</ValorDeducaoConstCivil>
    <ValorLiquido>970.00</ValorLiquido>
    <Observacao>Gerado via WebService</Observacao>
    <Complemento>Gerado via WebService</Complemento>
    <ListaServico>
      <DadosServico>
        <Discriminacao>Descrição do serviço prestado</Discriminacao>
        <ItemLei116>12.10</ItemLei116>
        <Quantidade>1</Quantidade>
        <ValorServico>1000.00</ValorServico>
        <ValorDesconto>0</ValorDesconto>
      </DadosServico>
    </ListaServico>
    <Versao>1.00</Versao>
  </DeclaracaoPrestacaoServico>
</GerarNfseEnvio>             
             
             
             */
        }

        #region not implemented
        public override NotaServico LoadXml(XDocument xml)
        {
            throw new NotImplementedException();
        }

        public override string WriteXmlNFSe(NotaServico nota, bool identado = true, bool showDeclaration = true)
        {
            throw new NotImplementedException();
        }

        public override string WriteXmlRps(NotaServico nota, bool identado = true, bool showDeclaration = true)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarCancelarNFSe(RetornoCancelar retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarCancelarNFSeLote(RetornoCancelarNFSeLote retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarConsultarLoteRps(RetornoConsultarLoteRps retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarConsultarSequencialRps(RetornoConsultarSequencialRps retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarConsultarSituacao(RetornoConsultarSituacao retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarEnviar(RetornoEnviar retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarEnviarSincrono(RetornoEnviar retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarSubstituirNFSe(RetornoSubstituirNFSe retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararCancelarNFSe(RetornoCancelar retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararCancelarNFSeLote(RetornoCancelarNFSeLote retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararConsultarLoteRps(RetornoConsultarLoteRps retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararConsultarSequencialRps(RetornoConsultarSequencialRps retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararConsultarSituacao(RetornoConsultarSituacao retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararEnviar(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararSubstituirNFSe(RetornoSubstituirNFSe retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoCancelarNFSe(RetornoCancelar retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoCancelarNFSeLote(RetornoCancelarNFSeLote retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoConsultarLoteRps(RetornoConsultarLoteRps retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoConsultarSequencialRps(RetornoConsultarSequencialRps retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoConsultarSituacao(RetornoConsultarSituacao retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoEnviar(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoEnviarSincrono(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoSubstituirNFSe(RetornoSubstituirNFSe retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
