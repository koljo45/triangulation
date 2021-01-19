#include <stdio.h>
#include <GL/freeglut.h>
#include <glm/common.hpp>
#include <glm/vec3.hpp>
#include <glm/geometric.hpp>
#include <glm/mat4x4.hpp>
#include <glm/gtc/matrix_access.hpp>
#include <glm/gtx/rotate_vector.hpp>
#include <glm/gtx/vector_angle.hpp>
#include <glm/ext/matrix_transform.hpp>

# define M_PI           3.14159265358979323846f
# define RAD_TO_ANGLE           180.0f/M_PI

const char* objectFileName = "aircraft747.obj";
const char* splineFileName = "spline.txt";
const int bufferSize = 100;
const float cameraMovementSpeed = 0.3f;
const float cameraRotationSpeed = 0.02f;
const int numPointsPerSegment = 5;
const int objectSpeed = 5; // how many points does the object travel in one second

glm::vec3 eyePosition = glm::vec3(5, 5, 0);
glm::vec3 viewPosition = glm::vec3(5, 5, 1);
glm::vec3 viewUp = glm::vec3(0, 1, 0);
glm::vec4 objectCenter = glm::vec4(0, 0, 0, 1);
glm::vec3 defaultObjectOrientation = glm::vec3(0, 0, 1);

int numVertices = 0;
int numTriangles = 0;
int numSplineControlPoints = 0;
glm::vec4* vertices;
int** triangles;
glm::vec4* splineControlPoints;
glm::vec4* splinePoints;
glm::vec3* splineTangents;
glm::vec3* splineDd;
glm::vec3* splineNormals;
glm::vec3* splineBinormals;

GLuint window;
GLuint width = 400, height = 300;
GLfloat minWindowDim;

void myDisplay();
void drawScene();
void myReshape(int width, int height);
void Update(int deltaTime);
void _internalUpdate();
glm::vec3 getSegmentPoint(int segment_index, float t, glm::vec3& tangent, glm::vec3& dd);
void calculateModelViewMatrix(glm::vec3* eyePos, glm::vec3* viewPos, glm::vec3* viewUp, glm::mat4x4* out);
void myKeyboard(unsigned char theKey, int mouseX, int mouseY);

glm::mat4 B_i_3 = glm::mat4{ -1, 3, -3, 1, 3, -6, 0, 4, -3, 3, 3, 1, 1, 0, 0, 0 };
glm::mat4x3 B_d_i_3 = glm::mat4x3{ -1, 2, -1, 3, -4, 0, -3, 2, 1, 1, 0, 0 };
glm::mat4x2 B_dd_i_3 = glm::mat4x2{ -1, 1, 3, -2, -3, 1, 1, 0 };
int timeStamp = 0;

int main(int argc, char** argv)
{
	char* buffer = new char[bufferSize];
	FILE* pFile = NULL;

	// ucitavanje krivulje
	printf("File %s open code: %d\n", splineFileName, fopen_s(&pFile, splineFileName, "r"));
	if (pFile == NULL) {
		printf("Could not load object! File %s does not exist!", objectFileName);
		return 1;
	}
	while (fgets(buffer, bufferSize, pFile) != NULL) {
		numSplineControlPoints++;
	}
	splineControlPoints = new glm::vec4[numSplineControlPoints];
	int numPoints = (numSplineControlPoints - 3) * numPointsPerSegment;
	splinePoints = new glm::vec4[numPoints];
	splineTangents = new glm::vec3[numPoints];
	splineDd = new glm::vec3[numPoints];
	splineNormals = new glm::vec3[numPoints];
	splineBinormals = new glm::vec3[numPoints];
	int s_cnt = 0;
	float x, y, z;

	fseek(pFile, 0, SEEK_SET);
	while (fscanf_s(pFile, "%f %f %f\n", &x, &y, &z) != EOF) {
		splineControlPoints[s_cnt++] = glm::vec4(x, y, z, 1);
	}

	fclose(pFile);

	// Ucitavanje objekta
	printf("File open code: %d\n", fopen_s(&pFile, objectFileName, "r"));

	if (pFile == NULL) {
		printf("Could not load object! File %s does not exist!", objectFileName);
		return 1;
	}

	while (fgets(buffer, bufferSize, pFile) != NULL) {
		if (buffer[0] == 'v')
			numVertices++;
		else if (buffer[0] == 'f')
			numTriangles++;
	}
	vertices = new glm::vec4[numVertices];
	triangles = new int* [numTriangles];

	fseek(pFile, 0, SEEK_SET);
	char c;
	int v_cnt = 0, p_cnt = 0;

	while (fgets(buffer, bufferSize, pFile) != NULL) {
		if (buffer[0] == 'v') {
			sscanf_s(buffer, "v %f %f %f\n", &x, &y, &z);
			glm::vec4 vec = glm::vec4(x, y, z, 1);
			vertices[v_cnt++] = vec;
			objectCenter += vec;
		}
		else if (buffer[0] == 'f') {
			triangles[p_cnt] = new int[3];
			sscanf_s(buffer, "f %d %d %d\n", triangles[p_cnt], triangles[p_cnt] + 1, triangles[p_cnt] + 2);
			triangles[p_cnt][0]--;
			triangles[p_cnt][1]--;
			triangles[p_cnt][2]--;
			p_cnt++;
		}
	}
	objectCenter /= numVertices;
	objectCenter.w = 1;

	delete[] buffer;

	fclose(pFile);

	// Calculate spline points
	for (int i = 0; i < numSplineControlPoints - 3; i++) {
		int offset = i * numPointsPerSegment;

		for (int t = 0; t < numPointsPerSegment; t++) {
			glm::vec3 tangent;
			glm::vec3 dd;
			glm::vec3 p = getSegmentPoint(i, (double)t / numPointsPerSegment, tangent, dd);
			glm::vec3 normal = glm::normalize(glm::cross(tangent, dd));

			int offsetIndex = offset + t;
			splinePoints[offsetIndex] = glm::vec4(p, 1);
			splineTangents[offsetIndex] = glm::normalize(tangent);
			splineDd[offsetIndex] = glm::normalize(dd);
			splineNormals[offsetIndex] = glm::normalize(normal);
			splineBinormals[offsetIndex] = glm::normalize(glm::cross(tangent, normal));
		}
	}

	glutInitDisplayMode(GLUT_DOUBLE | GLUT_RGB);
	glutInitWindowSize(width, height);
	glutInitWindowPosition(100, 100);
	glutInit(&argc, argv);

	window = glutCreateWindow("Glut OpenGL Tijelo");
	glutReshapeFunc(myReshape);
	glutDisplayFunc(myDisplay);
	glutKeyboardFunc(myKeyboard);
	glutIdleFunc(_internalUpdate);

	glutMainLoop();

	delete[] vertices;
	for (int i = 0; i < numTriangles; i++)
		delete[] triangles[i];
	return 0;
}

void myDisplay()
{
	//printf("Pozvan myDisplay()\n");
	glClearColor(1.0f, 1.0f, 1.0f, 1.0f); //  boja pozadine
	glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT); //brisanje nakon svake iscrtane linije

	glm::mat4x4 mMat;
	calculateModelViewMatrix(&eyePosition, &viewPosition, &viewUp, &mMat);
	mMat = glm::transpose(mMat);
	GLfloat* mMatFlat = new GLfloat[16];
	for (int i = 0; i < 16; i++) {
		mMatFlat[i] = mMat[i / 4][i % 4];
	}
	glMatrixMode(GL_MODELVIEW);			//	matrica pogleda
	glLoadMatrixf(mMatFlat);

	glFlush();
	drawScene();
	glutSwapBuffers();
}

//*********************************************************************************
//	Promjena velicine prozora.
//*********************************************************************************

void myReshape(int w, int h)
{
	//printf("Pozvan myReshape()\n");
	minWindowDim = width < height ? width : height;
	minWindowDim -= 10;
	width = w; height = h;               //promjena sirine i visine prozora
	glViewport(0, 0, width, height);	//  otvor u prozoru

	glMatrixMode(GL_PROJECTION);		//	matrica projekcije
	glLoadIdentity();					//	jedinicna matrica
	//gluOrtho2D(0, width, 0, height); 	//	okomita projekcija
	gluPerspective(90, width / height, 0.1, 100);
	glMatrixMode(GL_MODELVIEW);			//	matrica pogleda
	glLoadIdentity();					//	jedinicna matrica

	glClearColor(1.0f, 1.0f, 1.0f, 0.0f); // boja pozadine
	glClear(GL_COLOR_BUFFER_BIT);		//	brisanje pozadine
	glPointSize(1.0);					//	postavi velicinu tocke za liniju
	glColor3f(0.0f, 0.0f, 0.0f);		//	postavi boju linije
}

void _internalUpdate() {
	Update(glutGet(GLUT_ELAPSED_TIME) - timeStamp);
	timeStamp = glutGet(GLUT_ELAPSED_TIME);
}

float currentPoint = 0;
bool animationDone = false;
void Update(int deltaTime) {
	glutPostRedisplay();

	if (animationDone) {
		return;
	}

	float newPos = currentPoint + (float)deltaTime / 1000 * objectSpeed;
	if ((int)newPos >= (numSplineControlPoints - 3) * numPointsPerSegment - 1) {
		animationDone = true;
		return;
	}
	currentPoint = newPos;
}

void drawScene() {
	int floor = (int)currentPoint;
	int ceil = floor + 1;
	glm::vec4 positionOffset = splinePoints[floor] + (splinePoints[ceil] - splinePoints[floor]) * (currentPoint - floor) - objectCenter;
	glm::vec3 targetOrientation = glm::normalize(splineTangents[floor] + (splineTangents[ceil] - splineTangents[floor]) * (currentPoint - floor));
	positionOffset.w = 0;

	// crtanje krivulje
	glBegin(GL_LINE_STRIP);
	glColor3ub(0, 0, 255);
	for (int i = 0; i < numSplineControlPoints - 3; i++) {
		int offset = i * numPointsPerSegment;

		for (int t = 0; t < numPointsPerSegment; t++) {
			glm::vec4 pH = splinePoints[offset + t];

			glVertex3f(pH[0], pH[1], pH[2]);
		}
	}
	glEnd();

	// crtanje tangenti
	glBegin(GL_LINES);
	glColor3ub(255, 0, 0);
	for (int i = 0; i < numSplineControlPoints - 3; i++) {
		int offset = i * numPointsPerSegment;

		for (int t = 0; t < numPointsPerSegment; t++) {
			glm::vec3 tan = splineTangents[offset + t];
			glm::vec3 normal = splineNormals[offset + t];
			glm::vec3 binormal = splineBinormals[offset + t];

			glm::vec4 pH = splinePoints[offset + t];
			glm::vec4 pTan = pH + glm::vec4(tan, 1);
			glm::vec4 pNormal = pH + glm::vec4(normal, 1);
			glm::vec4 pBinormal = pH + glm::vec4(binormal, 1);

			glVertex3f(pH[0], pH[1], pH[2]);
			glVertex3f(pTan[0], pTan[1], pTan[2]);

			// iscrtavanje normale
			//glVertex3f(pH[0], pH[1], pH[2]);
			//glVertex3f(pNormal[0], pNormal[1], pNormal[2]);

			// binormale
			//glVertex3f(pH[0], pH[1], pH[2]);
			//glVertex3f(pBinormal[0], pBinormal[1], pBinormal[2]);
		}
	}
	glEnd();


	glMatrixMode(GL_MODELVIEW);
	glPushMatrix();
	glTranslatef(positionOffset.x, positionOffset.y, positionOffset.z);

	glm::vec3 axis = glm::cross(defaultObjectOrientation, targetOrientation);
	GLfloat angle = glm::angle(defaultObjectOrientation, targetOrientation);

	/*glm::mat3 rotationMat = glm::mat3();
	rotationMat = glm::row(rotationMat, 0, splineNormals[floor]);
	rotationMat = glm::row(rotationMat, 1, splineBinormals[floor]);
	rotationMat = glm::row(rotationMat, 2, splineTangents[floor]);
	rotationMat = glm::inverse(rotationMat);

	GLfloat* mRotFlat = new GLfloat[16];
	for (int i = 0; i < 3; i++) {
		for (int j = 0; j < 3; j++) {
			mRotFlat[i * 4 + j] = rotationMat[i][j];
		}
		mRotFlat[i * 4 + 3] = 0;
	}
	mRotFlat[12] = 0;
	mRotFlat[13] = 0;
	mRotFlat[14] = 0;
	mRotFlat[15] = 1;
	glMultMatrixf(mRotFlat);*/

	glRotatef(angle * RAD_TO_ANGLE, axis.x, axis.y, axis.z);

	// crtanje objekta
	glBegin(GL_TRIANGLES);
	glColor3ub(0, 0, 0);

	for (int i = 0; i < numTriangles; i++) {
		glm::vec4 p1 = (vertices[triangles[i][0]]);
		glm::vec4 p2 = (vertices[triangles[i][1]]);
		glm::vec4 p3 = (vertices[triangles[i][2]]);

		glVertex3f(p1[0], p1[1], p1[2]);
		glVertex3f(p2[0], p2[1], p2[2]);
		glVertex3f(p3[0], p3[1], p3[2]);
	}
	glEnd();
	glPopMatrix();
}

glm::vec4 polynom = glm::vec4(0, 0, 0, 1);
glm::mat3x4 segment = glm::mat3x4();
GLfloat polynom_factor = 1.0f / 6;
GLfloat polynom_d_factor = 1.0f / 10;


glm::vec3 getSegmentPoint(int segment_index, float t, glm::vec3& tangent, glm::vec3& dd)
{
	assert(segment_index < numSplineControlPoints - 3 && segment_index >= 0);
	assert(t >= 0 && t < 1);

	float t_pow = t;
	polynom[2] = t_pow;
	t_pow *= t;
	polynom[1] = t_pow;
	t_pow *= t;
	polynom[0] = t_pow;

	segment = glm::row(segment, 0, splineControlPoints[segment_index]);
	segment = glm::row(segment, 1, splineControlPoints[segment_index + 1]);
	segment = glm::row(segment, 2, splineControlPoints[segment_index + 2]);
	segment = glm::row(segment, 3, splineControlPoints[segment_index + 3]);

	tangent = glm::vec3(polynom[1], polynom[2], polynom[3]) * polynom_d_factor * B_d_i_3 * segment;
	dd = glm::vec2(polynom[2], polynom[3]) * B_dd_i_3 * segment;

	return polynom * polynom_factor * B_i_3 * segment;
}

void calculateModelViewMatrix(glm::vec3* eyePos, glm::vec3* viewPos, glm::vec3* viewUp, glm::mat4x4* out) {
	glm::mat4x4 T1 = glm::mat4x4{ 1.0f, 0.0f, 0.0f, -eyePos->x,
								  0.0f, 1.0f, 0.0f, -eyePos->y,
								  0.0f, 0.0f, 1.0f, -eyePos->z,
								  0.0f, 0.0f, 0.0f, 1.0f };
	glm::vec3 zAxis = glm::normalize(*viewPos - *eyePos);
	glm::vec3 xAxis = glm::cross(zAxis, glm::normalize(*viewUp));
	glm::vec3 yAxis = glm::cross(xAxis, zAxis);
	glm::mat4x4 Ruku = glm::mat4x4{ xAxis.x, xAxis.y, xAxis.z, 0.0f,
									yAxis.x, yAxis.y, yAxis.z, 0.0f,
									zAxis.x, zAxis.y, zAxis.z, 0.0f,
									0.0f, 0.0f, 0.0f, 1.0f };
	glm::mat4x4 Tz = glm::mat4x4{ 1.0f, 0.0f, 0.0f, 0.0f,
								  0.0f, 1.0f, 0.0f, 0.0f,
								  0.0f, 0.0f, -1.0f, 0.0f,
								  0.0f, 0.0f, 0.0f, 1.0f };
	*out = T1 * Ruku * Tz;
}

void myKeyboard(unsigned char theKey, int mouseX, int mouseY)
{
	glm::vec3 zAxis = viewPosition - eyePosition;
	glm::vec3 xAxis = glm::normalize(glm::cross(zAxis, viewUp));
	glm::vec3 yAxis = glm::normalize(glm::cross(xAxis, zAxis));
	glm::vec3 delta = glm::vec3(0, 0, 0);
	viewUp = yAxis;

	switch (theKey)
	{
	case 'w':
		delta = glm::normalize(zAxis) * cameraMovementSpeed;
		break;

	case 's':
		delta = glm::normalize(zAxis) * -cameraMovementSpeed;
		break;

	case 'd':
		delta = xAxis * cameraMovementSpeed;
		break;

	case 'a':
		delta = xAxis * -cameraMovementSpeed;
		break;

	case 'i':
		viewPosition = eyePosition + glm::rotate(zAxis, cameraRotationSpeed, xAxis);
		break;

	case 'k':
		viewPosition = eyePosition + glm::rotate(zAxis, -cameraRotationSpeed, xAxis);
		break;

	case 'j':
		viewPosition = eyePosition + glm::rotate(zAxis, cameraRotationSpeed, yAxis);
		break;

	case 'l':
		viewPosition = eyePosition + glm::rotate(zAxis, -cameraRotationSpeed, yAxis);
		break;

	case 'q':
		viewUp = glm::rotate(yAxis, -cameraRotationSpeed, zAxis);
		break;

	case 'e':
		viewUp = glm::rotate(yAxis, cameraRotationSpeed, zAxis);
		break;
	}

	eyePosition += delta;
	viewPosition += delta;
}