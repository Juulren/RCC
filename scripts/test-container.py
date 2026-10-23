"""Exercise the production image against a disposable PostgreSQL database. No real providers."""
import base64
import http.client
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess
import tempfile
import time
import urllib.request
import urllib.error
import uuid

ROOT = Path(__file__).resolve().parents[1]

def run(args, env=None):
    result = subprocess.run(args, cwd=ROOT, env=env, capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError('Fallo de herramienta en comprobacion de contenedor; salida privada retenida en el proceso, no se imprimen credenciales.')
    return result.stdout.strip()

def main():
    values = {}
    local = ROOT / '.env'
    if local.exists():
        for line in local.read_text(encoding='utf-8-sig').splitlines():
            if line and not line.startswith('#') and '=' in line:
                key, value = line.split('=', 1)
                values[key] = value.strip().strip('"')
    user = os.getenv('RCC_DOCKER_POSTGRES_USER', values.get('POSTGRES_USER', ''))
    password = os.getenv('RCC_DOCKER_POSTGRES_PASSWORD', values.get('POSTGRES_PASSWORD', ''))
    if not user or not password:
        raise RuntimeError('Configure .env local o RCC_DOCKER_POSTGRES_USER/PASSWORD de la instancia de pruebas.')
    database = 'rcc_docker_' + uuid.uuid4().hex
    container = 'rcc-smoke-' + uuid.uuid4().hex[:12]
    assert re.fullmatch(r'rcc_docker_[0-9a-f]{32}', database)
    image = os.getenv('RCC_DOCKER_IMAGE', 'rcc:module9')
    connection = f'Host=host.docker.internal;Port=5432;Database={database};Username="{user.replace(chr(34), chr(34)*2)}";Password="{password.replace(chr(34), chr(34)*2)}";Maximum Pool Size=10'
    env = os.environ.copy()
    env.update({'ConnectionStrings__Rcc': connection, 'Deployment__PublicOrigin': 'https://rcc-smoke.example.test', 'Deployment__RenderProxy': 'true', 'Deployment__ApplyMigrations': 'true', 'RENDER': 'true', 'RENDER_EXTERNAL_HOSTNAME': 'rcc-smoke.example.test', 'OpenAI__Enabled': 'false', 'Resend__Enabled': 'false', 'PORT': '8080'})
    # Test certificate only; never reuse production secrets in CI or inside an image layer.
    with tempfile.TemporaryDirectory(prefix='rcc-container-') as temporary:
        folder = Path(temporary)
        openssl = shutil.which('openssl')
        if not openssl:
            git_openssl = Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Git/usr/bin/openssl.exe'
            if git_openssl.exists(): openssl = str(git_openssl)
        if not openssl: raise RuntimeError('La prueba de contenedor requiere openssl (incluido en Git para Windows).')
        cert_password = secrets.token_hex(24)
        cert_env = os.environ | {'RCC_TEST_CERT_PASSWORD': cert_password}
        run([openssl, 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-days', '1', '-subj', '/CN=RCC container test', '-keyout', str(folder/'key.pem'), '-out', str(folder/'cert.pem')])
        run([openssl, 'pkcs12', '-export', '-inkey', str(folder/'key.pem'), '-in', str(folder/'cert.pem'), '-out', str(folder/'cert.pfx'), '-passout', 'env:RCC_TEST_CERT_PASSWORD'], env=cert_env)
        env['DataProtection__CertificateBase64'] = base64.b64encode((folder/'cert.pfx').read_bytes()).decode()
        env['DataProtection__CertificatePassword'] = cert_password
        # CI uses host PostgreSQL; local development uses Compose's socket without printing its password.
        ci = os.getenv('CI') == 'true'
        def sql(statement):
            if ci:
                command_env = os.environ | {'PGPASSWORD': password}
                return run(['psql', '-h', '127.0.0.1', '-U', user, '-d', 'postgres', '-v', 'ON_ERROR_STOP=1', '-c', statement], env=command_env)
            return run(['docker', 'compose', 'exec', '-T', 'postgres', 'psql', '-U', user, '-d', 'postgres', '-v', 'ON_ERROR_STOP=1', '-c', statement])
        created = False
        try:
            sql(f'CREATE DATABASE "{database}"'); created = True
            variables = [key for key in env if key in {'ConnectionStrings__Rcc','Deployment__PublicOrigin','Deployment__RenderProxy','Deployment__ApplyMigrations','RENDER','RENDER_EXTERNAL_HOSTNAME','DataProtection__CertificateBase64','DataProtection__CertificatePassword','OpenAI__Enabled','Resend__Enabled','PORT'}]
            options = ['--add-host', 'host.docker.internal:host-gateway']
            for key in variables: options += ['-e', key]
            run(['docker','run','--rm',*options,image,'--migrate'],env=env)
            run(['docker','run','-d','--name',container,'-p','127.0.0.1::8080',*options,image],env=env)
            port = int(run(['docker','port',container,'8080/tcp']).rsplit(':',1)[1])
            def request(path, extra=None):
                headers={'Host':'rcc-smoke.example.test','X-Forwarded-Proto':'https','X-Forwarded-For':'203.0.113.10'}
                headers.update(extra or {})
                return urllib.request.urlopen(urllib.request.Request(f'http://127.0.0.1:{port}'+path,headers=headers),timeout=10)
            ready = False
            for _ in range(60):
                try:
                    with request('/api/health/ready') as response:
                        ready = response.status == 200
                    if ready: break
                except (urllib.error.URLError, TimeoutError, ConnectionError, http.client.HTTPException): pass
                time.sleep(1)
            if not ready: raise RuntimeError('El contenedor no alcanzo readiness.')
            assert run(['docker','exec',container,'id','-u']) != '0'
            run(['docker','exec',container,'sh','-c','test ! -e /app/.env && test ! -d /app/.git && test ! -d /app/.local && test ! -d /app/node_modules'])
            with request('/api/status') as response: assert json.load(response)['module'] == 9
            for path in ['/', '/habitaciones', '/cuenta', '/reservar']:
                with request(path) as response: assert b'<div id="root">' in response.read()
            with request('/api/room-types') as response: assert response.status == 200
            with request('/api/auth/csrf') as response:
                assert json.load(response)['token']
                cookie = response.headers.get('Set-Cookie','').lower()
                assert '__host-rcc.csrf' in cookie and 'secure' in cookie and 'httponly' in cookie
            try: request('/api/not-implemented')
            except urllib.error.HTTPError as error: assert error.code == 404 and 'problem+json' in error.headers.get('Content-Type','')
            else: raise AssertionError('API desconocida no devolvio 404')
            print('Contenedor Production: migracion, usuario no root, exclusiones de secretos, SPA, catalogo, CSRF seguro y salud aprobados.')
        finally:
            subprocess.run(['docker','rm','-f',container],capture_output=True)
            if created: sql(f'DROP DATABASE "{database}" WITH (FORCE)')

if __name__ == '__main__': main()
