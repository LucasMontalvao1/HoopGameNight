import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, retry, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    retry({
      count: 2,
      delay: (error: HttpErrorResponse, retryCount: number) => {
        // Não fazer retry em erros 4xx (exceto 408 - Request Timeout)
        if (error.status >= 400 && error.status < 500 && error.status !== 408) {
          throw error;
        }

        // Delay exponencial: 1s, 2s, 4s...
        const delayMs = Math.min(1000 * Math.pow(2, retryCount - 1), 10000);
        console.log(`Tentativa ${retryCount} após ${delayMs}ms...`);

        return new Promise(resolve => setTimeout(resolve, delayMs));
      }
    }),
    catchError((error: HttpErrorResponse) => {
      let errorMessage = 'Erro desconhecido';

      if (error.error instanceof ErrorEvent) {
        // Erro do lado do cliente
        errorMessage = `Erro: ${error.error.message}`;
      } else {
        // Erro do lado do servidor
        switch (error.status) {
          case 0:
            errorMessage = 'Servidor não disponível. Verifique sua conexão ou se a API está rodando.';
            break;
          case 400:
            errorMessage = 'Requisição inválida';
            break;
          case 401:
            errorMessage = 'Não autorizado';
            break;
          case 403:
            errorMessage = 'Acesso negado';
            break;
          case 404:
            errorMessage = 'Recurso não encontrado';
            break;
          case 408:
            errorMessage = 'Tempo de requisição esgotado';
            break;
          case 429:
            errorMessage = 'Muitas requisições. Tente novamente mais tarde.';
            break;
          case 500:
            errorMessage = 'Erro interno do servidor';
            break;
          case 502:
            errorMessage = 'Bad Gateway';
            break;
          case 503:
            errorMessage = 'Serviço temporariamente indisponível';
            break;
          case 504:
            errorMessage = 'Gateway Timeout';
            break;
          default:
            errorMessage = `Erro ${error.status}: ${error.message}`;
        }
      }

      console.error('Erro HTTP interceptado:', {
        url: req.url,
        status: error.status,
        message: errorMessage,
        error
      });

      return throwError(() => ({
        ...error,
        userMessage: errorMessage
      }));
    })
  );
};
