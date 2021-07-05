﻿using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSocket.Uniswap.Infrastructure;
using WebSocket.Uniswap.Services;

namespace WebSocket.Uniswap.Middlewares
{
    internal class WebSocketConnectionsMiddleware
    {
        #region Fields
        private WebSocketConnectionsOptions _options;
        private IWebSocketConnectionsService _connectionsService;
        #endregion

        #region Constructor
        public WebSocketConnectionsMiddleware(RequestDelegate next, WebSocketConnectionsOptions options, IWebSocketConnectionsService connectionsService)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _connectionsService = connectionsService ?? throw new ArgumentNullException(nameof(connectionsService));
        }
        #endregion

        #region Methods
        public async Task Invoke(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                if (ValidateOrigin(context))
                {
                    System.Net.WebSockets.WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

                    WebSocketConnection webSocketConnection = new WebSocketConnection(webSocket, _options.ReceivePayloadBufferSize);
                    webSocketConnection.ReceiveText += async (sender, message) => { await webSocketConnection.SendAsync(message, CancellationToken.None); };
                    webSocketConnection.ReceiveCandleUpdate += async (sender, candle) => { await webSocketConnection.SendAsync(candle, CancellationToken.None); };
                    _connectionsService.AddConnection(webSocketConnection);

                    await webSocketConnection.ReceiveMessagesUntilCloseAsync();

                    if (webSocketConnection.CloseStatus.HasValue)
                    {
                        await webSocket.CloseAsync(webSocketConnection.CloseStatus.Value, webSocketConnection.CloseStatusDescription, CancellationToken.None);
                    }

                    _connectionsService.RemoveConnection(webSocketConnection.Id);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                }
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        private bool ValidateOrigin(HttpContext context)
        {
            return (_options.AllowedOrigins == null) || (_options.AllowedOrigins.Count == 0) || (_options.AllowedOrigins.Contains(context.Request.Headers["Origin"].ToString()));
        }
        #endregion
    }
}
