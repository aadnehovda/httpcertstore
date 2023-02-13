# Caddy HTTP certificate store

This is a simple implementation of a Caddy compatible certificate manager 
that gets certificates and keys out of the Windows Certificate Store.

Run the program and configure Caddy like this: 

```
{
  auto_https off
}

test.local.gd {
  tls {
    get_certificate http http://127.0.0.1:27510/LocalMachine/My
  }

  respond "Hello, world!"
}
```

Caddy will append some query string parameters to this URL which will be used to 
find a matching certificate:

* server_name
* signature_schemes
* cipher_suites

So whenever you browse to https://test.local.gd, Caddy will send a request to 
http://127.0.0.1:27510/LocalMachine/My?server_name=test.local.gd and this program will 
look up a mathing, valid certificate and respond with a complete PEM encoded chain
including the private key. 

https://caddyserver.com/docs/caddyfile/directives/tls#http

TODO: validate the signature schemes and ciphers when looking for a supported certificate.

You can also list certs in other locations of the Certificate Store:
* http://127.0.0.1:27510/CurrentUser/My
* http://127.0.0.1:27510/LocalMachine/My

This will bring up a list of the available certificates
```
[
  {
    "thumbprint": "34C2F1332E089BA12B52B942FD6E941249FFA58A",
    "subject": "OU=ahovda, O=mkcert development certificate",
    "expires": "2025-05-01T14:19:27+02:00",
    "signature": {
      "value": "1.2.840.113549.1.1.11",
      "friendlyName": "sha256RSA"
    },
    "publicKeyAlgorithm": {
      "value": "1.2.840.113549.1.1.1",
      "friendlyName": "RSA"
    },
    "subjectAlternativeName": [
      "*.local.gd"
    ]
  },
  {
    "thumbprint": "40EA139FF1236D2A35967A5A214930D96033E8BE",
    "subject": "CN=test.local.gd",
    "expires": "2033-02-03T11:10:11+01:00",
    "signature": {
      "value": "1.2.840.113549.1.1.10",
      "friendlyName": "RSASSA-PSS"
    },
    "publicKeyAlgorithm": {
      "value": "1.2.840.113549.1.1.1",
      "friendlyName": "RSA"
    },
    "subjectAlternativeName": []
  }
]
```

Append the thumbprint of the certificate to the URL to view the complete certificate including the private key.

http://127.0.0.1:27510/CurrentUser/My/34C2F1332E089BA12B52B942FD6E941249FFA58A

```
-----BEGIN RSA PRIVATE KEY-----
MIIEowIBAAKCAQEA0Ro4XYvn8VzFvKmvSdILM1CzPHjTmK0aLBMfbYEZsXf/UK31
ooxp/+X9GfDbBkD10nXF/Qnkf/JwT72oVCRBSXwtpe/lQorkICu6hPexOTGAeTTN
SX8WsrygPEISCFjkskTJI6U6HkLa80KNnkc2jrcsakkTBwtT40vQOeHXq4m8bHJE
DA7cRBka9jRUqpxPiOmyN0YW296kV0kaft4EcxKg433wGukmozjInMcZBulhM2a8
k59ZaDxd/nExtldNECtocepPFr3BUC3TcNR4ksQlQRsaOGQlnwr1U4PzI14hw5Ji
9/p73wZgoDRPPPU7O2qcSmLjc49JK5E4e/KriwIDAQABAoIBAQCQYY5x6VyqJ+jR
MEk8q6/YKuzX4IYaccwUis+0iOP4ymacisGFD3dRnrh45PoXA1u5imC/K8l/HyO1
q7tC/hWma/wJC/A9VjHWlNshXPYeC7Qt/OuSyBIgMGZjtStGYDrpbN2Fo1zR8E/G
kDnBBGMDgC9G5FYjEwtFct/AV9TXN/Aq6BWlq3Q3C/m0OTn+KzblJOi7QQl36No7
kKpUaFsEympE9zU3NikMBPZWfSfycHmFyXCZPGuAvzZlkXEQTsjRhfCPR6rlSvVu
ti8YGem0h6gVnofF1oFWgk8ZdGORpC3K8gR8AMs+rulZnselvjs4mzxLftRczwno
J5eZ7ZShAoGBAO0kUcyUCMkV9TkvNrVWFKodxHwdPC8Vzs4k7BswxTTenyQHdPoj
GB+MAXy/fpR4dQL706WyVXr6mLAmdvGjQ30G9y/vQXrnELl6IXNako28KAqQ5l7y
i/GJDYcEx+OkNVmUF+xxQIkD1Ohqj/P0XGUxjYv1nRfpdD/5YyiHMEIdAoGBAOG7
FHKX/faQMJhYej/c0QaET0pUulaatup4RRz3JElzyiOMCKG1CtfIulObRx+oIsqS
40XAeJISbgfstNr7S5vlup/N2pPNQKsX/a3ToevXwZL52Rl5fkf4jUX2MN1ijtGP
Cn+t9SCXkOr1FMrF3A+XGUvxUujHKV4DcWJt+bPHAoGAcuZoI+GOKDBVcvGnZlFh
DdQCgciAgqfkXgmRxNLqmmVLYl+ilBgw9ggWKGV4ZWO//kQaFmzD8iQ9mwvoEchz
SL75QeaBKZWPiu7cClJWNAp4tDXlCRgoKk2nmkX8vBWyK8cPmGZ/SGPs4vfO6/r4
Uaei16+YACADTYP+QYLPJRkCgYBHd2CA62gngP0xrol94J55Dri/0tVwB+mzkEJp
akGd08ACwxoUgGofaVi8kaPR/PyO4DR+Z/KEZLtbXPDhh8AnanNMlvMF6mz3/k99
kkxoi9Wof9vhOdrmVGX96XUNEmAfLsdnJx89o17pVJCxpjUG5pLGvW+/bRbGwhZK
0IDHDwKBgATQMqIQdC2k5Qb7aup/ZQZQM7qlFUhv1BHlIxhP0Q3D3f5IV/9KrgRG
BmcCG+XliIl2oSR4AMCH0NYbtVtu/Ne8ZZDBUbz5B/MRgHQWHkhIXXc1Np2kd6q6
7dCM5H6f9yVuoEwwxUuEPoN8wtRduBtKaLxkSgw7bCuuc5TBpFnk
-----END RSA PRIVATE KEY-----
[Version]
  V3

[Subject]
  CN=mkcert ahovda, OU=ahovda, O=mkcert development CA
  Simple Name: ahovda
  DNS Name: *.local.gd

[Issuer]
  CN=mkcert ahovda, OU=ahovda, O=mkcert development CA
  Simple Name: mkcert ahovda
  DNS Name: mkcert ahovda

[Serial Number]
  143F3B99867CA21E47B2E83EB942CCA8

[Not Before]
  01.02.2023 14:19:27

[Not After]
  01.05.2025 14:19:27

[Thumbprint]
  34C2F1332E089BA12B52B942FD6E941249FFA58A

[Signature Algorithm]
  sha256RSA(1.2.840.113549.1.1.11)

[Public Key]
  Algorithm: RSA
  Length: 2048
  Key Blob: 30 82 01 0a 02 82 01 01 00 d1 1a 38 5d 8b e7 f1 5c c5 bc a9 af 49 d2 0b 33 50 b3 3c 78 d3 98 ad 1a 2c 13 1f 6d 81 19 b1 77 ff 50 ad f5 a2 8c 69 ff e5 fd 19 f0 db 06 40 f5 d2 75 c5 fd 09 e4 7f f2 70 4f bd a8 54 24 41 49 7c 2d a5 ef e5 42 8a e4 20 2b ba 84 f7 b1 39 31 80 79 34 cd 49 7f 16 b2 bc a0 3c 42 12 08 58 e4 b2 44 c9 23 a5 3a 1e 42 da f3 42 8d 9e 47 36 8e b7 2c 6a 49 13 07 0b 53 e3 4b d0 39 e1 d7 ab 89 bc 6c 72 44 0c 0e dc 44 19 1a f6 34 54 aa 9c 4f 88 e9 b2 37 46 16 db de a4 57 49 1a 7e de 04 73 12 a0 e3 7d f0 1a e9 26 a3 38 c8 9c c7 19 06 e9 61 33 66 bc 93 9f 59 68 3c 5d fe 71 31 b6 57 4d 10 2b 68 71 ea 4f 16 bd c1 50 2d d3 70 d4 78 92 c4 25 41 1b 1a 38 64 25 9f 0a f5 53 83 f3 23 5e 21 c3 92 62 f7 fa 7b df 06 60 a0 34 4f 3c f5 3b 3b 6a 9c 4a 62 e3 73 8f 49 2b 91 38 7b f2 ab 8b 02 03 01 00 01
  Parameters: 05 00

[Private Key]

  Key Store: User
  Provider Name: Microsoft Software Key Storage Provider
  Provider type: 0
  Key Spec: 0

[Extensions]
* Key Usage(2.5.29.15):
  Digital Signature, Key Encipherment (a0)

* Enhanced Key Usage(2.5.29.37):
  Server Authentication (1.3.6.1.5.5.7.3.1)

* Authority Key Identifier(2.5.29.35):
  KeyID=088f8c61d227be097315ef3c889fa563df6e7cd8

* Subject Alternative Name(2.5.29.17):
  DNS Name=*.local.gd


-----BEGIN CERTIFICATE-----
MIIERDCCAqygAwIBAgIQFD87mYZ8oh5Hsug+uULMqDANBgkqhkiG9w0BAQsFADCB
gzEeMBwGA1UEChMVbWtjZXJ0IGRldmVsb3BtZW50IENBMSwwKgYDVQQLDCNTVkdc
YWhvdmRhQERFU0tUT1AtUlBPMFNSNSAoYWhvdmRhKTEzMDEGA1UEAwwqbWtjZXJ0
IFNWR1xhaG92ZGFAREVTS1RPUC1SUE8wU1I1IChhaG92ZGEpMB4XDTIzMDIwMTEz
MTkyN1oXDTI1MDUwMTEyMTkyN1owVzEnMCUGA1UEChMebWtjZXJ0IGRldmVsb3Bt
ZW50IGNlcnRpZmljYXRlMSwwKgYDVQQLDCNTVkdcYWhvdmRhQERFU0tUT1AtUlBP
MFNSNSAoYWhvdmRhKTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBANEa
OF2L5/Fcxbypr0nSCzNQszx405itGiwTH22BGbF3/1Ct9aKMaf/l/Rnw2wZA9dJ1
xf0J5H/ycE+9qFQkQUl8LaXv5UKK5CAruoT3sTkxgHk0zUl/FrK8oDxCEghY5LJE
ySOlOh5C2vNCjZ5HNo63LGpJEwcLU+NL0Dnh16uJvGxyRAwO3EQZGvY0VKqcT4jp
sjdGFtvepFdJGn7eBHMSoON98BrpJqM4yJzHGQbpYTNmvJOfWWg8Xf5xMbZXTRAr
aHHqTxa9wVAt03DUeJLEJUEbGjhkJZ8K9VOD8yNeIcOSYvf6e98GYKA0Tzz1Oztq
nEpi43OPSSuROHvyq4sCAwEAAaNfMF0wDgYDVR0PAQH/BAQDAgWgMBMGA1UdJQQM
MAoGCCsGAQUFBwMBMB8GA1UdIwQYMBaAFAiPjGHSJ74JcxXvPIifpWPfbnzYMBUG
A1UdEQQOMAyCCioubG9jYWwuZ2QwDQYJKoZIhvcNAQELBQADggGBAFqk5V00gr2a
yotr4ecciXiaou+gOzTB8xCpVd+v8ln7KzfgOl+SpzLhJlXuU0EDD2fyT10E5Lg5
dPem47dI5F9LhIWo84hIeMNyR4n8GpL0smnVlgGJg2XUIdtrvlsXGceQembfwXAq
2ReaEYO8x8Jz4af5Rbqk9xtrIAVYvca6EINjxRUCzOzoYzZiTCqUnkiJLA3DcrDq
RRMzwvk81QEEtPovWoFzjBiJm4aPUEOyIyF8RxENyW7MJR9SN/hBdUoQn0v8j/w2
6mViRgy/xRSRFd20MAiyG+NMUTB9nn/3NgSnu/2vmlqYGxGw4U4hJoJeIxC1otx4
wYVD6cInVvCKJ1gNjlosHr0jIgLtBpjSbLd7HUyW2/o+ng1bXH1ZVhUKeyGTnlct
QIjLPdSBHjFZ5Uy0uy4JzUEAWD9gD7MeeimFGQT2HNSdvzIcW643uq/GdClqEu1m
68JwoY+GaZK0MWn3N0LAul9GZb3n0SF1C4fmr/E2nlwZV0Gn9+tc/w==
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
MIIE2DCCA0CgAwIBAgIRAK+KyvL67eSe3HpCjBCIytQwDQYJKoZIhvcNAQELBQAw
gYMxHjAcBgNVBAoTFW1rY2VydCBkZXZlbG9wbWVudCBDQTEsMCoGA1UECwwjU1ZH
XGFob3ZkYUBERVNLVE9QLVJQTzBTUjUgKGFob3ZkYSkxMzAxBgNVBAMMKm1rY2Vy
dCBTVkdcYWhvdmRhQERFU0tUT1AtUlBPMFNSNSAoYWhvdmRhKTAeFw0yMzAyMDEx
MTMxNDZaFw0zMzAyMDExMTMxNDZaMIGDMR4wHAYDVQQKExVta2NlcnQgZGV2ZWxv
cG1lbnQgQ0ExLDAqBgNVBAsMI1NWR1xhaG92ZGFAREVTS1RPUC1SUE8wU1I1IChh
aG92ZGEpMTMwMQYDVQQDDCpta2NlcnQgU1ZHXGFob3ZkYUBERVNLVE9QLVJQTzBT
UjUgKGFob3ZkYSkwggGiMA0GCSqGSIb3DQEBAQUAA4IBjwAwggGKAoIBgQC75tZx
cGejY8Xfo/Y5rQv8reHrlB2GzQdIUTCbMcNe+9qx5yWKZQHBYtbHJz4ivJBdDJ2x
RtyzkDMjsp/Ds0VorlQdVQs9wquz34n9Mut8O+K0DkJQsxNotYMqv/xpasnyD2Jl
XV6vUtA4Z2P7zF7eJSIO+C+hvNpaNBsw9zeC3c82dGJQRf+dZlbP8bmQfCVI0CyG
5qQTykq17nP32fOdLCzP+bqCgW5GejcXDdVrHZ+znppeUK+m1BUPOprZlYr9caFB
5eHIlPpIH3tFKuUjP4ctAsd1ZsrH5KaYft0RXY3JeZJBPiqmd/tkk/94ReQMls15
5FjoL6vm138X6jqwOwrNtkhShg219dGbm8tfp8e6SbSCr+cVzxcY6a7ldaC9IT2w
rnDyace1w8DAa4+OmnA/jqlGUMxOZcKZvZsJkkZLU2IUC0UP5xWT8z5jpYN0U69g
IdAapp34QKHHjjpjLwwK+X1Vo5BwAgYutemrOY8aOUSMDz36xOSSARdTSc0CAwEA
AaNFMEMwDgYDVR0PAQH/BAQDAgIEMBIGA1UdEwEB/wQIMAYBAf8CAQAwHQYDVR0O
BBYEFAiPjGHSJ74JcxXvPIifpWPfbnzYMA0GCSqGSIb3DQEBCwUAA4IBgQBMDH8x
RwVbyeQX8sSqFViQtFs7JgdoxbPvQW+5X22Uz9yA69nxhe2wRskD0jNiQQz7vP/G
UNpJyW2KVCY4JwKjrlXKn5JjehLQaQWZzlhQqRBkstvaQB7ar3MMHyPUsYovNXPq
m8188t3rsbxkGqHCWLL3r+CVOe4RYQUokpU1t32b6wHR3NsbrlucKAUoyX0bTn1P
mrWqt/Adb9IxeP5l+ppp+KkreHkVrH3CWJrkJh0AKnrJYiHsc42Wp8fspFiW3f+8
nLnWFkRp/GVegNgjFyFF8tVhTmLat6gzRt9G6BpRLngLqfIvTssDrGq22/0Vuu0E
VwsMzS5pDsKSlWKcOMVKSRH1MpmDXgD9QsoWuL/sgbzDx9d09Rva/Fn4ZIDZ68q7
G/cNLjZKq7MgoMBFFwK6NPKyG3zDuEczvx/VDO+rfEl1aCRXGE7h2jjk1lQLluMV
yPaheNQwfj2GfN4TVCv2C9nTZu9mTtQbdtMbKYbdz+l65c8EiZftQVnuyRA=
-----END CERTIFICATE-----

```

