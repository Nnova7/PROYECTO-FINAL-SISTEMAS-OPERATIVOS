//PROYECTO FINAL -- SISTEMAS OPERATIVOS
/*    NOMBRES              APELLIDOS           ID
  DULCE MARIANA         ANDRADE OLVERA       376905
  ELIA GUADALUPE        ARTEAGA DELGADO      453701
  GEORGINA GUADALUPE    CALZADA GONZALEZ     246871
  VALERIA               RAMOS LÓPEZ          434209*/
#include <iostream>
#include <cstdlib>
#include <ctime>
#include <string>
#include <list>
#include <vector>
#include <iomanip>
#include <cmath>
#include <thread>
#include <chrono>
#include <algorithm>
#include <queue>
#include <mutex> // Para semáforos
#include <condition_variable> // Para semáforos

using namespace std;

#define numDeProcesos 10

vector<pair<int, int>> tiemposGlobales;  // Almacena el PID del proceso y su tiempo total

// Mutex global para proteger la salida a consola
mutex cout_mutex;
class Semaforo
{
    private:
    int contador;
    mutex mtx;
    condition_variable cv;

    public:
    Semaforo(int valorInicial) : contador(valorInicial) { }

    void wait()
    {
        unique_lock < mutex > lock (mtx) ;
        cv.wait(lock, [this]() { return contador > 0; });
        --contador;
    }

    void signal()
    {
        unique_lock < mutex > lock (mtx) ;
        ++contador;
        cv.notify_one();
    }
};

class Memoria
{
    public:
    // Aquí se implementa el buddy system ////////////////////////////////////////////
    int total;
    int usada;
    vector<int> bloques;
    // Se define un vector bloques que contiene los tamaños de los bloques disponibles. 
    // Al inicio, el bloque completo de memoria está disponible (total).
    Memoria(int total)
    {
        this->total = total;
        usada = 0;
        bloques.push_back(total);
    }

    bool asignarMemoria(int solicitud)
    {
        // Calcula el tamaño del bloque como la potencia de dos más cercana al tamaño solicitado.
        int tamanioNecesario = pow(2, ceil(log2(solicitud)));
        for (size_t i = 0; i < bloques.size(); ++i)
        {
            if (bloques[i] >= tamanioNecesario)
            {
                int bloqueActual = bloques[i];
                bloques.erase(bloques.begin() + i);

                while (bloqueActual / 2 >= tamanioNecesario)
                {
                    bloqueActual /= 2;
                    bloques.push_back(bloqueActual);
                }

                usada += tamanioNecesario;
                if (bloqueActual > tamanioNecesario)
                {
                    bloques.push_back(bloqueActual - tamanioNecesario);
                }

                bloques.erase(remove(bloques.begin(), bloques.end(), 0), bloques.end());
                representarMemoria(); // Mostrar estado de la memoria.
                return true;
            }
        }
        return false;
    }

    // Combinar bloques contiguos de tamaño idéntico en un bloque mayor.
    void combinarBloques()
    {
        sort(bloques.begin(), bloques.end()); // Ordenar bloques por tamaño

        for (size_t i = 0; i < bloques.size() - 1; ++i)
        {
            // Verifica si dos bloques consecutivos tienen el mismo tamaño
            if (bloques[i] == bloques[i + 1])
            {
                bloques[i] *= 2;                // Combinar bloques duplicando su tamaño
                bloques.erase(bloques.begin() + i + 1); // Eliminar el bloque combinado
                --i; // Retrocede para verificar si se pueden combinar más bloques
            }
        }
    }

    // Se calcula el tamaño del bloque liberado como la potencia de dos más cercana al
    // tamaño del proceso. Este bloque se devuelve al vector bloques.
    void liberarMemoria(int liberacion)
    {
        int tamanioLiberado = pow(2, ceil(log2(liberacion)));
        usada -= tamanioLiberado;
        bloques.push_back(tamanioLiberado);
        combinarBloques();
        representarMemoria(); // Mostrar estado de la memoria
    }

    void representarMemoria()
    {
        cout << "\n--- Representacion de la Memoria ---\n";
        cout << "Tamaño Total: " << total << " KB | Usada: " << usada << " KB | Libre: " << (total - usada) << " KB\n";
        cout << "Bloques:\n";

        sort(bloques.begin(), bloques.end()); // Ordena bloques para una visualización clara.
        vector<pair<int, string>> representacion; // Pares de tamaño y estado ("Libre" o "Ocupado").

        int memoriaUsada = usada;
        for (int bloque : bloques)
        {
            representacion.push_back({ bloque, "Libre"});
        memoriaUsada -= bloque;
    }
	
	    if (memoriaUsada > 0) {
	        representacion.push_back({memoriaUsada, "Ocupado"});
	    }
	
	    sort(representacion.begin(), representacion.end(), [](pair<int, string> & a, pair<int, string> & b) {
    return a.first > b.first;
});

for (const auto &par : representacion) {
	        cout << setw(10) << par.first << " KB - " << par.second << endl;
	    }
	    cout << string(40, '-') << endl;
	}
    
    // Muestra el estado actual de los bloques disponibles en el sistema.
    void imprimirBloques()
{
    cout << "Bloques disponibles: ";
    for (int bloque : bloques){
            cout << bloque << " KB ";
        }
        cout << endl;
    }
}; ///////////////////////////////////////////////////////////////////////////////////

class Proceso
{
    public:
    int tiempo;
    int memoria;
    int pid;
    string estado;
    int tiempoTotal;

    Proceso() : tiempo(0), memoria(0), pid(0), estado("Ready"), tiempoTotal(0) { }

    Proceso(int tiempo, int memoria, int pid)
    : tiempo(tiempo), memoria(memoria), pid(pid), estado("Ready"), tiempoTotal(0) { }

    void imprimirProceso() const {
        cout << left << setw(10) << pid
             << setw(15) << tiempo
             << setw(15) << memoria
             << setw(10) << estado
             << setw(15) << tiempoTotal << endl;
    }
};

void manejarInterrupcion(Proceso &p, Memoria &memoria)
{
    cout << "Interrupcion: Proceso " << p.pid << " finalizado.\n";
    memoria.liberarMemoria(p.memoria);
}

void roundRobin(vector<Proceso> &procesos, int quantum, Memoria &memoria, queue<Proceso> &procesosSuspendidos, Semaforo &semaforo)
{
    cout << left << setw(10) << "PID"
    << setw(15) << "Tiempo"
    << setw(15) << "Memoria"
    << setw(10) << "Estado"
    << setw(15) << "Tiempo Total" << endl;  // Mostramos el tiempo total
    cout << string(50, '-') << endl;
    while (!procesos.empty() || !procesosSuspendidos.empty())
    {
        if (procesos.empty() && !procesosSuspendidos.empty())
        {
            procesos.push_back(procesosSuspendidos.front());
            procesosSuspendidos.pop();
        }

        Proceso p = procesos.front();
        procesos.erase(procesos.begin());

        semaforo.wait();
        if (p.tiempo > quantum)
        {
            p.tiempo -= quantum;
            p.estado = "Running";
            p.tiempoTotal += quantum;  // Acumulamos el tiempo ejecutado
            p.imprimirProceso();
            procesos.push_back(p);
        }
        else
        {
            p.estado = "Go";
            p.tiempo = 0; // Asegurar que el tiempo final sea 0
            p.tiempoTotal += p.tiempo;  // Agregar el tiempo restante al tiempo total
            //aqui guardar en la lista el ultimo tiempo que tuvo el proceso
            tiemposGlobales.push_back({ p.pid, p.tiempoTotal});
            p.imprimirProceso();
            manejarInterrupcion(p, memoria);
        }
        semaforo.signal();

        // Mostrar tabla actualizada
        cout << "\n--- Tabla Actualizada ---\n";
        cout << left << setw(10) << "PID"
             << setw(15) << "Tiempo"
             << setw(15) << "Memoria"
             << setw(10) << "Estado"
             << setw(15) << "Tiempo Total" << endl;  // Mostramos el tiempo total
        cout << string(50, '-') << endl;
        for (const auto &proceso : procesos) {
            cout << left << setw(10) << proceso.pid
                 << setw(15) << proceso.tiempo
                 << setw(15) << proceso.memoria
                 << setw(10) << proceso.estado
                 << setw(15) << proceso.tiempoTotal << endl;  // Mostrar el tiempo total
        }

        this_thread::sleep_for(chrono::milliseconds(500));
    }
}

void problemaFilosofosComensales()
{
    const int numFilosofos = 5;
    vector<mutex> tenedores(numFilosofos);
    cout << left << setw(15) << "Filosofo"
         << setw(20) << "Estado"
         << setw(15) << "Intentos" << endl;
    cout << string(50, '-') << endl;

    auto filosofo = [&](int id) {
        for (int i = 0; i < 5; ++i)
        {
            {
                lock_guard<mutex> guard(cout_mutex);  // Protege la impresión
                cout << left << setw(20) << ("Filosofo " + to_string(id))
                     << setw(20) << "Pensando"
                     << setw(15) << (i + 1) << endl;
            }
            this_thread::sleep_for(chrono::milliseconds(1000 + rand() % 500));

            int tenedorIzquierdo = id;
            int tenedorDerecho = (id + 1) % numFilosofos;

            // Lock de los tenedores
            lock (tenedores[tenedorIzquierdo], tenedores[tenedorDerecho]);
        lock_guard<mutex> lockIzquierdo(tenedores[tenedorIzquierdo], adopt_lock);
        lock_guard<mutex> lockDerecho(tenedores[tenedorDerecho], adopt_lock);

        {
            lock_guard<mutex> guard(cout_mutex);  // Protege la impresión
            cout << left << setw(20) << ("Filosofo " + to_string(id))
                 << setw(20) << "Comiendo"
                 << setw(15) << (i + 1) << endl;
        }
        this_thread::sleep_for(chrono::milliseconds(1000 + rand() % 500));

        {
            lock_guard<mutex> guard(cout_mutex);  // Protege la impresión
            cout << left << setw(20) << ("Filosofo " + to_string(id))
                 << setw(20) << "Termino de Comer"
                 << setw(15) << (i + 1) << endl;
        }
    }
};

vector<thread> filosofos;
for (int i = 0; i < numFilosofos; ++i)
{
    filosofos.emplace_back(filosofo, i);
}

for (auto & f : filosofos)
{
    f.join();
}
}

void graficas()
{
    // Recorrer cada par (PID, tiempoTotal) de la lista global de tiempos
    for (const auto&t : tiemposGlobales) {
        int pid = t.first;           // El PID del proceso
int tiempoAcumulado = t.second;  // El tiempo total acumulado del proceso

// Mostrar el PID y los tiempos registrados
cout << left << setw(10) << pid << " | ";

// Mostrar la barra de progreso normalizada
int barraLongitud = static_cast<int>(50.0 * tiempoAcumulado / 1000.0);  // Normaliza la longitud de la barra (ajustar el divisor según lo que desees)

//cout << "\033[1;32m";  // Color verde (puedes cambiarlo si lo prefieres
// Imprimir la barra de progreso
for (int i = 0; i < barraLongitud; ++i)
{
    cout << "=";
}

// Resetea el color y muestra el tiempo acumulado
//cout << "\033[0m";  
cout << " (" << tiempoAcumulado << " ms)" << endl;
    }
}

int main()
{
    srand(time(NULL));

    Memoria memoria(1024);
    vector<Proceso> procesos;
    queue<Proceso> procesosSuspendidos;
    Semaforo semaforo(1);

    cout << "\n--- Estado Inicial de la Memoria ---\n";
    memoria.representarMemoria();

    for (int i = 0; i < numDeProcesos; ++i)
    {
        int tiempo = 50 + rand() % 451;
        int memoriaUsada = 1 + rand() % 256;
        int pid = i + 1;

        if (memoria.asignarMemoria(memoriaUsada))
        {
            procesos.emplace_back(tiempo, memoriaUsada, pid);
        }
        else
        {
            cout << "Proceso " << pid << " suspendido por falta de memoria.\n";
            procesosSuspendidos.emplace(tiempo, memoriaUsada, pid);
        }
    }

    cout << "\n--- Procesos Generados ---\n";
    cout << left << setw(10) << "PID"
         << setw(15) << "Tiempo"
         << setw(15) << "Memoria"
         << setw(10) << "Estado" << endl;
    cout << string(50, '-') << endl;

    for (const auto &p : procesos) {
        cout << left << setw(10) << p.pid
             << setw(15) << p.tiempo
             << setw(15) << p.memoria
             << setw(10) << p.estado << endl;
    }

    memoria.imprimirBloques();

int quantum = 100;
roundRobin(procesos, quantum, memoria, procesosSuspendidos, semaforo);
cout << "\n--- Estado Final de la Memoria ---\n";
memoria.imprimirBloques();

cout << "\n--- Problema de los Filosofos Comensales ---\n";
problemaFilosofosComensales();

cout << "\n--- Graficas ---\n";
graficas();

return 0;
}