# First-generation clients

The central server still answers first-generation clients, which send their requests without a length (`handshakeV1` and `updateV1` in `ClientRequestHandler`), so that they can update themselves. This support should be removed once no such client is left.
