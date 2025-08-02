// ***********************************************************************
// Assembly         : OpenAC.Net.NFSe
// Author           : Fabio Dias (ZCoder Sistemas)
// Created          : 08-01-2025
//
// ***********************************************************************
// <copyright file="ProviderBase.cs" company="OpenAC .Net">
//		        		   The MIT License (MIT)
//	     		Copyright (c) 2014 - 2025 Projeto OpenAC .Net
//
//	 Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//	 The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//	 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// </copyright>
// <summary></summary>
// ***********************************************************************

using OpenAC.Net.Core.Extensions;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.NFSe.Commom;
using OpenAC.Net.NFSe.Commom.Interface;
using OpenAC.Net.NFSe.Commom.Model;
using OpenAC.Net.NFSe.Commom.Types;
using OpenAC.Net.NFSe.Configuracao;
using OpenAC.Net.NFSe.Nota;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OpenAC.Net.NFSe.Providers.Centi;

internal sealed class ProviderCenti : ProviderABRASF200
{
    public ProviderCenti(ConfigNFSe config, OpenMunicipioNFSe municipio) : base(config, municipio)
    {
        Name = "Centi";
        Versao = VersaoNFSe.ve200;
    }

    protected override IServiceClient GetClient(TipoUrl tipo) => new CentiServiceClient(this, tipo);

    protected override void TratarRetornoConsultarNFSeRps(RetornoConsultarNFSeRps retornoWebservice, NotaServicoCollection notas)
    {
        // Analisa mensagem de retorno
        string textRet = retornoWebservice.XmlRetorno.HtmlDecode();
        if (!textRet.Contains("ConsultarNfseRpsResposta"))
        {
            Match? match = Regex.Match(textRet, @"<body[^>]*>(.*?)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            string erro = match.Success ? match.Groups[1].Value.Trim() : textRet;
            retornoWebservice.Erros.Add(new EventoRetorno
            {
                Codigo = "999",
                Descricao = erro,
            });
            return;
        }
        var xmlRet = XDocument.Parse(textRet);
        MensagemErro(retornoWebservice, xmlRet, "ConsultarNfseRpsResposta");
        if (retornoWebservice.Erros.Any()) return;
        var compNfse = xmlRet.ElementAnyNs("ConsultarNfseRpsResposta")?.ElementAnyNs("CompNfse");
        if (compNfse == null)
        {
            retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "Nota Fiscal não encontrada! (CompNfse)" });
            return;
        }
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

            var nfseCancelamento = compNfse.ElementAnyNs("NfseCancelamento");

            if (nfseCancelamento != null)
            {
                nota.Situacao = SituacaoNFSeRps.Cancelado;

                var confirmacaoCancelamento = nfseCancelamento
                    .ElementAnyNs("Confirmacao");

                if (confirmacaoCancelamento != null)
                {
                    var pedido = confirmacaoCancelamento.ElementAnyNs("Pedido");

                    if (pedido != null)
                    {
                        var codigoCancelamento = pedido
                            .ElementAnyNs("InfPedidoCancelamento")
                            .ElementAnyNs("CodigoCancelamento")
                            .GetValue<string>();

                        nota.Cancelamento.Pedido.CodigoCancelamento = codigoCancelamento;

                        nota.Cancelamento.Signature = DFeSignature.Load(pedido.ElementAnyNs("Signature").ToString());
                    }
                }

                nota.Cancelamento.DataHora = confirmacaoCancelamento
                    .ElementAnyNs("DataHora")
                    .GetValue<DateTime>();
            }
        }

        retornoWebservice.Nota = nota;
        retornoWebservice.Sucesso = true;
    }

    protected override void TratarRetornoEnviarSincrono(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
    {
        // Analisa mensagem de retorno
        string textRet = retornoWebservice.XmlRetorno.HtmlDecode();
        if (!textRet.Contains("ConsultarNfseRpsResposta"))
        {
            Match? match = Regex.Match(textRet, @"<body[^>]*>(.*?)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            string erro = match.Success ? match.Groups[1].Value.Trim() : textRet;
            retornoWebservice.Erros.Add(new EventoRetorno
            {
                Codigo = "999",
                Descricao = erro,
            });
            return;
        }
        var xmlRet = XDocument.Parse(textRet);
        MensagemErro(retornoWebservice, xmlRet, "EnviarLoteRpsSincronoResposta");
        if (retornoWebservice.Erros.Any()) return;

        retornoWebservice.Data = xmlRet.Root?.ElementAnyNs("DataRecebimento")?.GetValue<DateTime>() ?? DateTime.MinValue;
        retornoWebservice.Protocolo = xmlRet.Root?.ElementAnyNs("Protocolo")?.GetValue<string>() ?? string.Empty;
        retornoWebservice.Sucesso = !retornoWebservice.Protocolo.IsEmpty();
        MensagemErro(retornoWebservice, xmlRet, "EnviarLoteRpsSincronoResposta");

        if (!retornoWebservice.Sucesso) return;

        var listaNfse = xmlRet.Root.ElementAnyNs("ListaNfse");

        if (listaNfse == null)
        {
            retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "Lista de NFSe não encontrada! (ListaNfse)" });
            return;
        }

        foreach (var compNfse in listaNfse.ElementsAnyNs("CompNfse"))
        {
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

            nota.Protocolo = retornoWebservice.Protocolo;
        }
    }


    protected override void PrepararEnviarSincrono(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
    {
        if (retornoWebservice.Lote == 0) retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "Lote não informado." });
        if (notas.Count == 0) retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "RPS não informado." });
        if (retornoWebservice.Erros.Any()) return;
        var xmlLoteRps = new StringBuilder();
        foreach (var nota in notas)
        {
            var xmlRps = WriteXmlRps(nota, false, false);
            xmlLoteRps.Append(xmlRps);
            GravarRpsEmDisco(xmlRps, $"Rps-{nota.IdentificacaoRps.DataEmissao:yyyyMMdd}-{nota.IdentificacaoRps.Numero}.xml", nota.IdentificacaoRps.DataEmissao);
        }

        var xmlLote = new StringBuilder();
        xmlLote.Append($"<GerarNfseEnvio {GetNamespace()}>");
        xmlLote.Append(xmlLoteRps);
        xmlLote.Append("</GerarNfseEnvio>");
        retornoWebservice.XmlEnvio = xmlLote.ToString();
    }

    protected override void AssinarEnviarSincrono(RetornoEnviar retornoWebservice)
    {
        retornoWebservice.XmlEnvio = XmlSigning.AssinarXmlTodos(retornoWebservice.XmlEnvio, "Rps", "", Certificado);
    }

}


/*
{"xml": 
<GerarNfseEnvio xmlns=\"http://www.centi.com.br/files/nfse.xsd\">
    <Rps>
        <InfDeclaracaoPrestacaoServico>
            <Rps Id=\"ZBUDULKHKV\">
                <IdentificacaoRps>
                    <Numero>507</Numero>
                    <Serie>2</Serie>
                    <Tipo>1</Tipo>
                </IdentificacaoRps>
                <DataEmissao>2020-06-08T11:11:00</DataEmissao>
                <Status>1</Status>
            </Rps>
            <Competencia>2020-06-08</Competencia>
            <Servico>
                <Valores>
                    <ValorServicos>210.00</ValorServicos>
                    <ValorDeducoes>0.00</ValorDeducoes>
                    <ValorPis>0.00</ValorPis>
                    <ValorCofins>0.00</ValorCofins>
                    <ValorInss>0.00</ValorInss>
                    <ValorIr>0.00</ValorIr>
                    <ValorCsll>0.00</ValorCsll>
                    <OutrasRetencoes>0.00</OutrasRetencoes>
                    <ValorIss>6.30</ValorIss>
                    <Aliquota>3.00</Aliquota>
                    <DescontoIncondicionado>0.00</DescontoIncondicionado>
                    <DescontoCondicionado>0.00</DescontoCondicionado>
                </Valores>
                <IssRetido>2</IssRetido>
		<ResponsavelRetencao>1</ResponsavelRetencao>
                <ItemListaServico>1402</ItemListaServico>
		<CodigoCnae>4541203</CodigoCnae>
                <CodigoTributacaoMunicipio>5218805</CodigoTributacaoMunicipio>
                <Discriminacao>MAO DE OBRA</Discriminacao>
                <CodigoMunicipio>5218805</CodigoMunicipio>
                <CodigoPais>1058</CodigoPais>
                <ExigibilidadeISS>1</ExigibilidadeISS>
                <MunicipioIncidencia>5218805</MunicipioIncidencia>
		<Observacao>OBSERVAÇÃO DIVERSA</Observacao>
            </Servico>
            <Prestador>
                <CpfCnpj>
                    <Cnpj>11111111111111</Cnpj>
                </CpfCnpj>
                <InscricaoMunicipal>11111</InscricaoMunicipal>
            </Prestador>
            <Tomador>
                <IdentificacaoTomador>
                    <CpfCnpj>
                        <Cnpj>22222222222</Cnpj>
                    </CpfCnpj>
                    <InscricaoMunicipal>2222222</InscricaoMunicipal>
                </IdentificacaoTomador>
                <RazaoSocial>RAZAO TOMADOR</RazaoSocial>
                <Endereco>
                    <Endereco>RUA MARIO SILVA</Endereco>
                    <Numero>128</Numero>
                    <Complemento>AP 403</Complemento>
                    <Bairro>CENTRO</Bairro>
                    <CodigoMunicipio>5218805</CodigoMunicipio>
                    <Uf>GO</Uf>
                    <CodigoPais>1058</CodigoPais>
                    <Cep>98700000</Cep>
                </Endereco>
                <Contato>
                    <Telefone>55984084884</Telefone>
                    <Email>tomador@email.com</Email>
                </Contato>
            </Tomador>
	    <OptanteSimplesNacional>2</OptanteSimplesNacional>
            <RegimeEspecialTributacao>6</RegimeEspecialTributacao>
            <IncentivoFiscal>2</IncentivoFiscal>
        </InfDeclaracaoPrestacaoServico>
        <Signature
            xmlns=\"http://www.w3.org/2000/09/xmldsig#\">
            <SignedInfo>
                <CanonicalizationMethod Algorithm=\"http://www.w3.org/TR/2001/REC-xml-c14n-20010315\" />
                <SignatureMethod Algorithm=\"http://www.w3.org/2000/09/xmldsig#rsa-sha1\" />
                <Reference URI=\"\">
                    <Transforms>
                        <Transform Algorithm=\"http://www.w3.org/2000/09/xmldsig#enveloped-signature\" />
                        <Transform Algorithm=\"http://www.w3.org/TR/2001/REC-xml-c14n-20010315\" />
                    </Transforms>
                    <DigestMethod Algorithm=\"http://www.w3.org/2000/09/xmldsig#sha1\" />
                    <DigestValue>hosJ/grjyN1ccwIGeNaT8uBTns8=</DigestValue>
                </Reference>
            </SignedInfo>
            <SignatureValue>K38/jQ75dOJZyvsf78XDVR9S/tdpn+ihCR77C8zTSIhUGo9JvvVnkqrga6PlcUewoDkFgSMxos7QoZFXADnyJUhCO1vGQWsy2pcz1ktQyv9klIwVsoGgXEVC0WWRtsp0A5dm5WmUgni0BKynVABISTZsouX+ShgmW+rMJReDXqhiiEKs1OGN6IspFErmDsRaOmHtKeKQ/a1b76ZTt2sf1/+4kOPRx1IdO2rvX9BN2kXqN4yHkJIyxu9Gnf5LyOMUNuix1Jbi2mxf34T6nMo/KKlb3lfkyebJ/8paZOBy5CFtqTqUcOoRjyxSEtJhwJWJQKr4+4vbiIqonPTIt4sk6w==</SignatureValue>
            <KeyInfo>
                <X509Data>
                    <X509Certificate>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</X509Certificate>
                </X509Data>
            </KeyInfo>
        </Signature>
    </Rps>
</GerarNfseEnvio>", "usuario": "xxxxxxxxxxxxxxx", "senha": "xxxxxxx} 
 
 

cpf fabio: 002.679.141-21
cnpj empresa: 42.318.248/0001-45
im: 26543



 */